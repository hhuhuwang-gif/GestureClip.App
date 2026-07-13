using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using GestureClip.Core.Abstractions;
using Microsoft.Extensions.Logging;

namespace GestureClip.Infrastructure.Updates;

public sealed class GitHubReleaseUpdateInstallerService : IUpdateInstallerService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<GitHubReleaseUpdateInstallerService> _logger;

    public GitHubReleaseUpdateInstallerService(HttpClient httpClient, ILogger<GitHubReleaseUpdateInstallerService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        GitHubReleaseUpdateCheckService.EnsureUserAgent(_httpClient);
    }

    public async Task StartCoverUpdateAsync(CancellationToken cancellationToken = default)
    {
        var checkService = new GitHubReleaseUpdateCheckService(_httpClient);
        var release = await checkService.GetLatestReleaseAsync(cancellationToken);
        var asset = release.Assets.FirstOrDefault(item =>
            item.Name.EndsWith(GitHubReleaseUpdateCheckService.PackageAssetSuffix, StringComparison.OrdinalIgnoreCase) &&
            item.Name.Contains("GestureClip", StringComparison.OrdinalIgnoreCase) &&
            !item.Name.Contains("Setup", StringComparison.OrdinalIgnoreCase));
        if (asset is null || string.IsNullOrWhiteSpace(asset.BrowserDownloadUrl))
        {
            // Fallback: any win-x64 zip that is not named Setup
            asset = release.Assets.FirstOrDefault(item =>
                item.Name.EndsWith(GitHubReleaseUpdateCheckService.PackageAssetSuffix, StringComparison.OrdinalIgnoreCase) &&
                !item.Name.Contains("Setup", StringComparison.OrdinalIgnoreCase));
        }

        if (asset is null || string.IsNullOrWhiteSpace(asset.BrowserDownloadUrl))
        {
            throw new InvalidOperationException("最新版本没有 Windows x64 便携安装包（*-win-x64.zip）。");
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

        _logger.LogInformation("Downloading portable update package. Asset={AssetName}", asset.Name);
        await GitHubUpdateTransport.DownloadToFileAsync(asset.BrowserDownloadUrl, downloadPath, cancellationToken);
        ZipFile.ExtractToDirectory(downloadPath, extractPath, overwriteFiles: true);

        var installDirectory = AppContext.BaseDirectory.TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar);
        var executableName = Path.GetFileName(
            Environment.ProcessPath ?? Path.Combine(installDirectory, "GestureClip.exe"));
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
}
