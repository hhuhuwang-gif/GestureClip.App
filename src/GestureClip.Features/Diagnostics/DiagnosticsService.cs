using System.Reflection;
using System.Text;
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

    public DiagnosticsService(
        AppPathProvider paths,
        ISystemPermissionService permissionService,
        IClipboardService clipboardService,
        IMouseGestureService mouseGestureService,
        IGlobalHotkeyService globalHotkeyService,
        IKeyboardInputSender keyboardInputSender)
    {
        _paths = paths;
        _permissionService = permissionService;
        _clipboardService = clipboardService;
        _mouseGestureService = mouseGestureService;
        _globalHotkeyService = globalHotkeyService;
        _keyboardInputSender = keyboardInputSender;
    }

    public Task<DiagnosticsSnapshot> GetSnapshotAsync(CancellationToken cancellationToken)
    {
        var gestureDiagnostics = _mouseGestureService.Diagnostics;
        var snapshot = new DiagnosticsSnapshot(
            GetVersion(),
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

        return "Recent error exists. See logs for technical details.";
    }
}
