namespace GestureClip.Core.Gestures;

public sealed record GestureOptions(
    int TriggerThreshold,
    int SegmentThreshold,
    int MaxDurationMs,
    int MinGesturePoints);
