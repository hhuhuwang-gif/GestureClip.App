using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Text.Json.Serialization;
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
        var release = await new GitHubReleaseUpdateCheckService(_httpClient).GetLatestReleaseAsync(cancellationToken);
        var asset = release.Assets.FirstOrDefault(item =>
            item.Name.EndsWith(GitHubReleaseUpdateCheckService.PackageAssetSuffix, StringComparison.OrdinalIgnoreCase));
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
        try
        {
            await DownloadFileWithClientAsync(_httpClient, url, destinationPath, cancellationToken);
        }
        catch (HttpRequestException)
        {
            using var directClient = UpdateHttpClientFactory.CreateDirectClient();
            await DownloadFileWithClientAsync(directClient, url, destinationPath, cancellationToken);
        }
    }

    private static async Task DownloadFileWithClientAsync(HttpClient httpClient, string url, string destinationPath, CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var destination = File.Create(destinationPath);
        await source.CopyToAsync(destination, cancellationToken);
    }

}
