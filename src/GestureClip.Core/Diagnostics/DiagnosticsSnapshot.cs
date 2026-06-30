namespace GestureClip.Core.Diagnostics;

public sealed record DiagnosticsSnapshot(
    string AppVersion,
    string DatabasePath,
    string LogDirectory,
    bool IsAdministrator,
    bool ClipboardCaptureEnabled,
    bool GestureEnabled,
    string HotkeyStatus,
    string HookStatus,
    string? LastGesturePattern,
    string? LastGestureAction,
    string? LastKeyboardInputStatus,
    string? LastErrorSummary);
