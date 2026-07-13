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
        var selected = ReleasePackageSelector.Select(release.Assets);
        if (selected is null)
        {
            throw new InvalidOperationException(
                "最新版本没有可用的 Windows 安装包（需要 Setup.exe、Setup zip 或 win-x64 zip）。");
        }

        var updateRoot = Path.Combine(Path.GetTempPath(), "GestureClip", "update", release.TagName);
        var downloadPath = Path.Combine(updateRoot, selected.Name);
        Directory.CreateDirectory(updateRoot);

        _logger.LogInformation(
            "Downloading update package. Kind={Kind} Asset={AssetName}",
            selected.Kind,
            selected.Name);
        await GitHubUpdateTransport.DownloadToFileAsync(selected.BrowserDownloadUrl, downloadPath, cancellationToken);

        switch (selected.Kind)
        {
            case ReleasePackageSelector.PackageKind.SetupExe:
                await LaunchSetupExeAsync(downloadPath, cancellationToken);
                break;
            case ReleasePackageSelector.PackageKind.SetupZip:
                await LaunchSetupZipAsync(downloadPath, updateRoot, cancellationToken);
                break;
            case ReleasePackageSelector.PackageKind.PortableZip:
                await LaunchPortableZipCoverUpdateAsync(downloadPath, updateRoot, cancellationToken);
                break;
            default:
                throw new InvalidOperationException("未知的安装包类型。");
        }
    }

    private async Task LaunchSetupExeAsync(string setupExePath, CancellationToken cancellationToken)
    {
        // Prefer silent upgrade when Inno-style flags are supported; UI setup still works without flags.
        var args = DetectSilentArgs(setupExePath);
        Process.Start(new ProcessStartInfo
        {
            FileName = setupExePath,
            Arguments = args,
            UseShellExecute = true,
            WorkingDirectory = Path.GetDirectoryName(setupExePath) ?? Environment.CurrentDirectory
        });
        _logger.LogInformation("Setup.exe launched for upgrade. Args={Args}", args);
        await Task.CompletedTask;
    }

    private async Task LaunchSetupZipAsync(string zipPath, string updateRoot, CancellationToken cancellationToken)
    {
        var extractPath = Path.Combine(updateRoot, "setup-package");
        if (Directory.Exists(extractPath))
        {
            Directory.Delete(extractPath, recursive: true);
        }

        ZipFile.ExtractToDirectory(zipPath, extractPath, overwriteFiles: true);

        var setupCmd = Directory.EnumerateFiles(extractPath, "Setup.cmd", SearchOption.AllDirectories).FirstOrDefault()
            ?? Directory.EnumerateFiles(extractPath, "install.ps1", SearchOption.AllDirectories).FirstOrDefault();
        if (setupCmd is null)
        {
            throw new InvalidOperationException("Setup 包内找不到 Setup.cmd / install.ps1。");
        }

        if (setupCmd.EndsWith(".ps1", StringComparison.OrdinalIgnoreCase))
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{setupCmd}\" -Silent",
                UseShellExecute = true,
                WorkingDirectory = Path.GetDirectoryName(setupCmd)!
            });
        }
        else
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = setupCmd,
                Arguments = "/S",
                UseShellExecute = true,
                WorkingDirectory = Path.GetDirectoryName(setupCmd)!
            });
        }

        _logger.LogInformation("Setup package installer launched from {SetupPath}.", setupCmd);
        await Task.CompletedTask;
    }

    private async Task LaunchPortableZipCoverUpdateAsync(
        string zipPath,
        string updateRoot,
        CancellationToken cancellationToken)
    {
        var extractPath = Path.Combine(updateRoot, "package");
        if (Directory.Exists(extractPath))
        {
            Directory.Delete(extractPath, recursive: true);
        }

        ZipFile.ExtractToDirectory(zipPath, extractPath, overwriteFiles: true);

        var installDirectory = AppContext.BaseDirectory.TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar);
        var executableName = Path.GetFileName(
            Environment.ProcessPath ?? Path.Combine(installDirectory, "GestureClip.exe"));
        var scriptPath = Path.Combine(updateRoot, "install-update.cmd");
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
        _logger.LogInformation("Legacy portable cover update started from {ScriptPath}.", scriptPath);
    }

    private static string DetectSilentArgs(string setupExePath)
    {
        // Inno Setup common silent flags. Harmless if ignored by unknown setup hosts.
        var name = Path.GetFileName(setupExePath);
        if (name.Contains("Setup", StringComparison.OrdinalIgnoreCase))
        {
            return "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /CLOSEAPPLICATIONS";
        }

        return "";
    }
}
