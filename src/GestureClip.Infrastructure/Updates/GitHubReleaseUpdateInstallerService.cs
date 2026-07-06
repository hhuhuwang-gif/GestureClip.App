using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using GestureClip.Core.Abstractions;
using Microsoft.Extensions.Logging;

namespace GestureClip.Infrastructure.Updates;

public sealed class GitHubReleaseUpdateInstallerService : IUpdateInstallerService
{
    private const string LatestReleaseApiUrl = "https://api.github.com/repos/hhuhuwang-gif/GestureClip.App/releases/latest";
    private const string PackageAssetSuffix = "-win-x64.zip";

    private readonly HttpClient _httpClient;
    private readonly ILogger<GitHubReleaseUpdateInstallerService> _logger;

    public GitHubReleaseUpdateInstallerService(HttpClient httpClient, ILogger<GitHubReleaseUpdateInstallerService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        if (!_httpClient.DefaultRequestHeaders.UserAgent.Any())
        {
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("GestureClip-Updater");
        }
    }

    public async Task StartCoverUpdateAsync(CancellationToken cancellationToken = default)
    {
        var release = await _httpClient.GetFromJsonAsync<GitHubRelease>(LatestReleaseApiUrl, cancellationToken)
            ?? throw new InvalidOperationException("无法读取 GitHub 最新版本信息。");
        var asset = release.Assets.FirstOrDefault(item =>
            item.Name.EndsWith(PackageAssetSuffix, StringComparison.OrdinalIgnoreCase));
        if (asset is null)
        {
            throw new InvalidOperationException("最新版本没有 Windows x64 安装包。");
        }

        var updateRoot = Path.Combine(Path.GetTempPath(), "GestureClip", "update", release.TagName);
        var downloadPath = Path.Combine(updateRoot, asset.Name);
        var extractPath = Path.Combine(updateRoot, "package");
        var scriptPath = Path.Combine(updateRoot, "install-update.cmd");
        Directory.CreateDirectory(updateRoot);
        if (Directory.Exists(extractPath))
        {
            Directory.Delete(extractPath, recursive: true);
        }

        await DownloadFileAsync(asset.BrowserDownloadUrl, downloadPath, cancellationToken);
        ZipFile.ExtractToDirectory(downloadPath, extractPath, overwriteFiles: true);

        var installDirectory = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var executableName = Path.GetFileName(Environment.ProcessPath ?? Path.Combine(installDirectory, "GestureClip.exe"));
        await File.WriteAllTextAsync(
            scriptPath,
            UpdateInstallerScriptBuilder.Build(extractPath, installDirectory, executableName),
            cancellationToken);

        Process.Start(new ProcessStartInfo
        {
            FileName = scriptPath,
            UseShellExecute = true,
            WorkingDirectory = updateRoot,
            WindowStyle = ProcessWindowStyle.Normal
        });
        _logger.LogInformation("Cover update installer started from {ScriptPath}.", scriptPath);
    }

    private async Task DownloadFileAsync(string url, string destinationPath, CancellationToken cancellationToken)
    {
        using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var destination = File.Create(destinationPath);
        await source.CopyToAsync(destination, cancellationToken);
    }

    private sealed record GitHubRelease(
        [property: JsonPropertyName("tag_name")] string TagName,
        [property: JsonPropertyName("assets")] IReadOnlyList<GitHubReleaseAsset> Assets);

    private sealed record GitHubReleaseAsset(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("browser_download_url")] string BrowserDownloadUrl);
}
