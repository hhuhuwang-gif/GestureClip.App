using GestureClip.Core.Gestures;

namespace GestureClip.Features.Gestures;

public sealed record GestureSettings(
    bool Enabled,
    bool ShowOverlay,
    bool CloseWindowEnabled,
    bool DebugEnabled,
    GesturePreset Preset,
    GestureOptions Options);
