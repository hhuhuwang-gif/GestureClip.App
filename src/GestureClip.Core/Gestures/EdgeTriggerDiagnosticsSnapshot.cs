namespace GestureClip.Core.Gestures;

public sealed record EdgeTriggerDiagnosticsSnapshot(
    bool IsEnabled,
    string LastSource,
    string LastPosition,
    BuiltInGestureAction LastAction,
    string LastReason,
    DateTimeOffset? LastEventAt,
    DateTimeOffset? CooldownUntil);
