using System.Reflection;
using System.Text;
using System.IO.Compression;
using GestureClip.Core.Abstractions;
using GestureClip.Core.Diagnostics;
using GestureClip.Core.SystemInfo;
using GestureClip.Infrastructure.Paths;

namespace GestureClip.Features.Diagnostics;

public sealed class DiagnosticsService : IDiagnosticsService
{
    private readonly AppPathProvider _paths;
    private readonly ISystemPermissionService _permissionService;
    private readonly IClipboardService _clipboardService;
    private readonly IMouseGestureService _mouseGestureService;
    private readonly IGlobalHotkeyService _globalHotkeyService;
    private readonly IKeyboardInputSender _keyboardInputSender;
    private readonly IAppEnvironment _appEnvironment;

    public DiagnosticsService(
        AppPathProvider paths,
        ISystemPermissionService permissionService,
        IClipboardService clipboardService,
        IMouseGestureService mouseGestureService,
        IGlobalHotkeyService globalHotkeyService,
        IKeyboardInputSender keyboardInputSender,
        IAppEnvironment appEnvironment)
    {
        _paths = paths;
        _permissionService = permissionService;
        _clipboardService = clipboardService;
        _mouseGestureService = mouseGestureService;
        _globalHotkeyService = globalHotkeyService;
        _keyboardInputSender = keyboardInputSender;
        _appEnvironment = appEnvironment;
    }

    public Task<DiagnosticsSnapshot> GetSnapshotAsync(CancellationToken cancellationToken)
    {
        var gestureDiagnostics = _mouseGestureService.Diagnostics;
        var snapshot = new DiagnosticsSnapshot(
            GetVersion(),
            _appEnvironment.ApplicationPath,
            _paths.DatabasePath,
            _paths.LogDirectory,
            _permissionService.GetCurrentStatus() == PermissionStatus.Administrator,
            _clipboardService.IsCaptureEnabled,
            _mouseGestureService.IsEnabled,
            _globalHotkeyService.Status.DisplayText,
            gestureDiagnostics.HookStatus,
            gestureDiagnostics.LastPattern,
            gestureDiagnostics.LastAction.ToString(),
            _keyboardInputSender.LastStatus,
            SanitizeSummary(gestureDiagnostics.LastError));

        return Task.FromResult(snapshot);
    }

    public async Task<string> BuildReportAsync(CancellationToken cancellationToken)
    {
        var snapshot = await GetSnapshotAsync(cancellationToken);
        var builder = new StringBuilder();
        builder.AppendLine("GestureClip Diagnostics");
        builder.AppendLine($"Version: {snapshot.AppVersion}");
        builder.AppendLine($"ApplicationPath: {snapshot.ApplicationPath}");
        builder.AppendLine($"DatabasePath: {snapshot.DatabasePath}");
        builder.AppendLine($"LogDirectory: {snapshot.LogDirectory}");
        builder.AppendLine($"IsAdministrator: {snapshot.IsAdministrator}");
        builder.AppendLine($"ClipboardCaptureEnabled: {snapshot.ClipboardCaptureEnabled}");
        builder.AppendLine($"GestureEnabled: {snapshot.GestureEnabled}");
        builder.AppendLine($"HotkeyStatus: {snapshot.HotkeyStatus}");
        builder.AppendLine($"HookStatus: {snapshot.HookStatus}");
        builder.AppendLine($"LastGesturePattern: {snapshot.LastGesturePattern ?? "-"}");
        builder.AppendLine($"LastGestureAction: {snapshot.LastGestureAction ?? "-"}");
        builder.AppendLine($"LastKeyboardInputStatus: {snapshot.LastKeyboardInputStatus ?? "-"}");
        builder.AppendLine($"LastErrorSummary: {snapshot.LastErrorSummary ?? "-"}");
        return builder.ToString();
    }

    public async Task<string> ExportPackageAsync(CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_paths.RootDirectory);
        var fileName = $"GestureClip-Diagnostics-{DateTime.Now:yyyyMMdd-HHmmss}.zip";
        var packagePath = Path.Combine(_paths.RootDirectory, fileName);

        if (File.Exists(packagePath))
        {
            File.Delete(packagePath);
        }

        await using var stream = new FileStream(packagePath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Create);

        var reportEntry = archive.CreateEntry("diagnostics.txt");
        await using (var reportStream = reportEntry.Open())
        await using (var writer = new StreamWriter(reportStream, Encoding.UTF8))
        {
            await writer.WriteAsync(await BuildReportAsync(cancellationToken));
        }

        if (Directory.Exists(_paths.LogDirectory))
        {
            var logFiles = Directory.EnumerateFiles(_paths.LogDirectory, "*.log")
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .Take(5)
                .Select(logFile => new FileInfo(logFile))
                .ToList();

            if (logFiles.Count > 0)
            {
                var manifestEntry = archive.CreateEntry("logs/manifest.txt", CompressionLevel.Optimal);
                await using var manifestStream = manifestEntry.Open();
                await using var manifestWriter = new StreamWriter(manifestStream, Encoding.UTF8);
                await manifestWriter.WriteLineAsync("Raw log contents are excluded from diagnostics packages to avoid leaking clipboard contents.");

                foreach (var logFile in logFiles)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await manifestWriter.WriteLineAsync(
                        $"{logFile.Name}\tLastWriteUtc={logFile.LastWriteTimeUtc:O}\tBytes={logFile.Length}");
                }
            }
        }

        return packagePath;
    }

    private static string GetVersion()
    {
        return Assembly.GetEntryAssembly()?.GetName().Version?.ToString()
            ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString()
            ?? "unknown";
    }

    private static string? SanitizeSummary(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return "Recent error exists. See local logs for technical details.";
    }
}
