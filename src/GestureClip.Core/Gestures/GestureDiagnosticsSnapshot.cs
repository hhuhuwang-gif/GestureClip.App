namespace GestureClip.Core.Gestures;

public sealed record GestureDiagnosticsSnapshot(
    string HookStatus,
    GestureRuntimeState State,
    string? LastPattern,
    BuiltInGestureAction LastAction,
    string? LastError,
    DateTimeOffset? LastEventAt,
    bool IsDisabledByEnvironment);
