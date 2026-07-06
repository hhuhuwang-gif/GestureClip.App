using System.Net.Http;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json.Serialization;
using GestureClip.Core.Abstractions;
using GestureClip.Core.Updates;

namespace GestureClip.Infrastructure.Updates;

public sealed class GitHubReleaseUpdateCheckService : IUpdateCheckService
{
    public const string LatestReleaseApiUrl = "https://api.github.com/repos/hhuhuwang-gif/GestureClip.App/releases/latest";
    public const string PackageAssetSuffix = "-win-x64.zip";

    private readonly HttpClient _httpClient;

    public GitHubReleaseUpdateCheckService(HttpClient httpClient)
    {
        _httpClient = httpClient;
        EnsureUserAgent(_httpClient);
    }

    public async Task<UpdateCheckResult> CheckLatestAsync(CancellationToken cancellationToken = default)
    {
        var release = await GetLatestReleaseAsync(cancellationToken);
        var currentVersion = GetCurrentVersion();
        var latestVersion = release.TagName;
        return new UpdateCheckResult(
            currentVersion,
            latestVersion,
            release.Name,
            release.HtmlUrl,
            release.Body ?? string.Empty,
            UpdateVersionComparer.IsNewerRelease(currentVersion, latestVersion));
    }

    public async Task<GitHubRelease> GetLatestReleaseAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await GetLatestReleaseWithClientAsync(_httpClient, cancellationToken);
        }
        catch (HttpRequestException)
        {
            using var directClient = UpdateHttpClientFactory.CreateDirectClient();
            return await GetLatestReleaseWithClientAsync(directClient, cancellationToken);
        }
    }

    private static async Task<GitHubRelease> GetLatestReleaseWithClientAsync(HttpClient httpClient, CancellationToken cancellationToken)
    {
        return await httpClient.GetFromJsonAsync<GitHubRelease>(LatestReleaseApiUrl, cancellationToken)
            ?? throw new InvalidOperationException("无法读取 GitHub 最新版本信息。");
    }

    public static string GetCurrentVersion()
    {
        return Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? "0.0.0";
    }

    public static void EnsureUserAgent(HttpClient httpClient)
    {
        if (!httpClient.DefaultRequestHeaders.UserAgent.Any())
        {
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("GestureClip-Updater");
        }
    }

    public sealed record GitHubRelease(
        [property: JsonPropertyName("tag_name")] string TagName,
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("html_url")] string HtmlUrl,
        [property: JsonPropertyName("body")] string? Body,
        [property: JsonPropertyName("assets")] IReadOnlyList<GitHubReleaseAsset> Assets);

    public sealed record GitHubReleaseAsset(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("browser_download_url")] string BrowserDownloadUrl);
}
