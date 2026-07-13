using System.IO;
using System.Net.Http;

namespace GestureClip.Infrastructure.Updates;

/// <summary>
/// Resilient GitHub access for update check/download.
/// Tries system proxy, direct, and public reverse-proxy mirrors with short per-attempt timeouts.
/// Remembers the last successful route for the process lifetime.
/// </summary>
public static class GitHubUpdateTransport
{
    public const string OfficialLatestReleaseApi =
        "https://api.github.com/repos/hhuhuwang-gif/GestureClip.App/releases/latest";

    public const string OfficialReleasesPage =
        "https://github.com/hhuhuwang-gif/GestureClip.App/releases/latest";

    /// <summary>Public reverse proxies that forward GitHub URLs (used only after official fails).</summary>
    private static readonly string[] MirrorPrefixes =
    [
        "https://ghfast.top/",
        "https://gh-proxy.com/",
        "https://mirror.ghproxy.com/",
    ];

    private static string? _preferredRouteKey;

    public static IReadOnlyList<UpdateHttpRoute> BuildApiRoutes()
    {
        var routes = new List<UpdateHttpRoute>
        {
            new("官方 API · 系统代理", OfficialLatestReleaseApi, UseProxy: true, Timeout: TimeSpan.FromSeconds(12)),
            new("官方 API · 直连", OfficialLatestReleaseApi, UseProxy: false, Timeout: TimeSpan.FromSeconds(12)),
        };

        foreach (var prefix in MirrorPrefixes)
        {
            var url = prefix + OfficialLatestReleaseApi;
            routes.Add(new($"镜像 API · 直连 ({HostOf(prefix)})", url, UseProxy: false, Timeout: TimeSpan.FromSeconds(15)));
            routes.Add(new($"镜像 API · 系统代理 ({HostOf(prefix)})", url, UseProxy: true, Timeout: TimeSpan.FromSeconds(15)));
        }

        return PreferLastSuccess(routes);
    }

    public static IReadOnlyList<UpdateHttpRoute> BuildDownloadRoutes(string officialDownloadUrl)
    {
        if (string.IsNullOrWhiteSpace(officialDownloadUrl))
        {
            throw new ArgumentException("Download URL is empty.", nameof(officialDownloadUrl));
        }

        var routes = new List<UpdateHttpRoute>
        {
            new("官方下载 · 系统代理", officialDownloadUrl, UseProxy: true, Timeout: TimeSpan.FromMinutes(3)),
            new("官方下载 · 直连", officialDownloadUrl, UseProxy: false, Timeout: TimeSpan.FromMinutes(3)),
        };

        foreach (var prefix in MirrorPrefixes)
        {
            var url = prefix + officialDownloadUrl;
            routes.Add(new($"镜像下载 · 直连 ({HostOf(prefix)})", url, UseProxy: false, Timeout: TimeSpan.FromMinutes(3)));
            routes.Add(new($"镜像下载 · 系统代理 ({HostOf(prefix)})", url, UseProxy: true, Timeout: TimeSpan.FromMinutes(3)));
        }

        return PreferLastSuccess(routes);
    }

    public static async Task<HttpResponseMessage> GetAsync(
        IReadOnlyList<UpdateHttpRoute> routes,
        HttpCompletionOption completionOption,
        CancellationToken cancellationToken)
    {
        if (routes.Count == 0)
        {
            throw new InvalidOperationException("没有可用的更新网络路径。");
        }

        var errors = new List<Exception>();
        foreach (var route in routes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                using var client = UpdateHttpClientFactory.CreateClient(route.UseProxy, route.Timeout);
                var response = await client.GetAsync(route.Url, completionOption, cancellationToken).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    var statusCode = (int)response.StatusCode;
                    var reason = response.ReasonPhrase;
                    var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                    response.Dispose();
                    var snippet = string.IsNullOrWhiteSpace(body)
                        ? reason
                        : body.Length > 200 ? body[..200] : body;
                    throw new HttpRequestException(
                        $"路径 {route.Label} 返回 {statusCode}：{snippet}");
                }

                RememberSuccess(route);
                return response;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException or InvalidOperationException)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }

                errors.Add(new InvalidOperationException($"{route.Label} 失败：{Summarize(ex)}", ex));
            }
        }

        throw new AggregateException(
            "已尝试系统代理、直连与镜像加速，仍无法访问 GitHub 更新服务。",
            errors);
    }

    public static async Task DownloadToFileAsync(
        string officialDownloadUrl,
        string destinationPath,
        CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var routes = BuildDownloadRoutes(officialDownloadUrl);
        var errors = new List<Exception>();

        foreach (var route in routes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var partialPath = destinationPath + ".part";
            try
            {
                TryDelete(partialPath);

                using var client = UpdateHttpClientFactory.CreateClient(route.UseProxy, route.Timeout);
                using var response = await client
                    .GetAsync(route.Url, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                    .ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    var statusCode = (int)response.StatusCode;
                    var reason = response.ReasonPhrase;
                    var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                    var snippet = string.IsNullOrWhiteSpace(body)
                        ? reason
                        : body.Length > 200 ? body[..200] : body;
                    throw new HttpRequestException(
                        $"路径 {route.Label} 返回 {statusCode}：{snippet}");
                }

                await using (var source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false))
                await using (var destination = new FileStream(
                    partialPath,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.None,
                    bufferSize: 81920,
                    useAsync: true))
                {
                    await source.CopyToAsync(destination, cancellationToken).ConfigureAwait(false);
                }

                if (File.Exists(destinationPath))
                {
                    File.Delete(destinationPath);
                }

                File.Move(partialPath, destinationPath);
                RememberSuccess(route);
                return;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException or InvalidOperationException)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    TryDelete(partialPath);
                    throw;
                }

                TryDelete(partialPath);
                errors.Add(new InvalidOperationException($"{route.Label} 失败：{Summarize(ex)}", ex));
            }
        }

        throw new AggregateException(
            "已尝试系统代理、直连与镜像加速，仍无法下载更新包。",
            errors);
    }

    /// <summary>Expose preferred key for tests.</summary>
    public static string? PreferredRouteKey => _preferredRouteKey;

    public static void ResetPreferredRouteForTests() => _preferredRouteKey = null;

    public static void RememberSuccessForTests(UpdateHttpRoute route) => RememberSuccess(route);

    private static IReadOnlyList<UpdateHttpRoute> PreferLastSuccess(List<UpdateHttpRoute> routes)
    {
        var preferredKey = Volatile.Read(ref _preferredRouteKey);
        if (string.IsNullOrEmpty(preferredKey))
        {
            return routes;
        }

        var index = routes.FindIndex(route => RouteKey(route) == preferredKey);
        if (index <= 0)
        {
            return routes;
        }

        var reordered = new List<UpdateHttpRoute>(routes.Count) { routes[index] };
        for (var i = 0; i < routes.Count; i++)
        {
            if (i != index)
            {
                reordered.Add(routes[i]);
            }
        }

        return reordered;
    }

    private static void RememberSuccess(UpdateHttpRoute route)
    {
        Volatile.Write(ref _preferredRouteKey, RouteKey(route));
    }

    private static string RouteKey(UpdateHttpRoute route) =>
        $"{(route.UseProxy ? "proxy" : "direct")}|{route.Url}";

    private static string HostOf(string prefix)
    {
        try
        {
            return new Uri(prefix).Host;
        }
        catch
        {
            return prefix;
        }
    }

    private static string Summarize(Exception ex)
    {
        var message = ex.Message;
        if (string.IsNullOrWhiteSpace(message) && ex.InnerException is not null)
        {
            message = ex.InnerException.Message;
        }

        return string.IsNullOrWhiteSpace(message) ? ex.GetType().Name : message.Trim();
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // best effort
        }
    }
}

public sealed record UpdateHttpRoute(string Label, string Url, bool UseProxy, TimeSpan Timeout);
