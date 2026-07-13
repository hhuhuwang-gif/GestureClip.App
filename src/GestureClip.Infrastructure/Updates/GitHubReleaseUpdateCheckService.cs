using System.Net.Http;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json.Serialization;
using GestureClip.Core.Abstractions;
using GestureClip.Core.Updates;

namespace GestureClip.Infrastructure.Updates;

public sealed class GitHubReleaseUpdateCheckService : IUpdateCheckService
{
    public const string LatestReleaseApiUrl = GitHubUpdateTransport.OfficialLatestReleaseApi;
    /// <summary>Legacy portable package suffix (cover-update fallback).</summary>
    public const string PackageAssetSuffix = "-win-x64.zip";
    /// <summary>Preferred end-user installer markers on GitHub Releases.</summary>
    public const string SetupPackageMarker = "Setup";

    // Kept for DI compatibility; transport creates per-route clients.
    private readonly HttpClient _httpClient;

    public GitHubReleaseUpdateCheckService(HttpClient httpClient)
    {
        _httpClient = httpClient;
        UpdateHttpClientFactory.ConfigureHeaders(_httpClient);
    }

    public async Task<UpdateCheckResult> CheckLatestAsync(CancellationToken cancellationToken = default)
    {
        var release = await GetLatestReleaseAsync(cancellationToken);
        var currentVersion = GetCurrentVersion();
        var latestVersion = release.TagName;
        return new UpdateCheckResult(
            currentVersion,
            latestVersion,
            string.IsNullOrWhiteSpace(release.Name) ? latestVersion : release.Name,
            string.IsNullOrWhiteSpace(release.HtmlUrl)
                ? GitHubUpdateTransport.OfficialReleasesPage
                : release.HtmlUrl,
            release.Body ?? string.Empty,
            UpdateVersionComparer.IsNewerRelease(currentVersion, latestVersion));
    }

    public async Task<GitHubRelease> GetLatestReleaseAsync(CancellationToken cancellationToken = default)
    {
        using var response = await GitHubUpdateTransport.GetAsync(
            GitHubUpdateTransport.BuildApiRoutes(),
            HttpCompletionOption.ResponseContentRead,
            cancellationToken);

        var release = await response.Content.ReadFromJsonAsync<GitHubRelease>(cancellationToken: cancellationToken);
        if (release is null || string.IsNullOrWhiteSpace(release.TagName))
        {
            throw new InvalidOperationException("无法读取 GitHub 最新版本信息。");
        }

        return release;
    }

    public static string GetCurrentVersion()
    {
        var version = Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? "0.0.0";
        var plus = version.IndexOf('+');
        return plus > 0 ? version[..plus] : version;
    }

    public static void EnsureUserAgent(HttpClient httpClient) => UpdateHttpClientFactory.ConfigureHeaders(httpClient);

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
