namespace GestureClip.Core.Gestures;

public sealed record MouseHookEvent(
    MouseHookEventType Type,
    int X,
    int Y,
    DateTimeOffset Time,
    bool IsInjected = false);
