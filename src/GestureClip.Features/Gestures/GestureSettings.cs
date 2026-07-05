using GestureClip.Core.Gestures;

namespace GestureClip.Features.Gestures;

public sealed record GestureSettings(
    bool Enabled,
    bool ShowOverlay,
    bool CloseWindowEnabled,
    bool DebugEnabled,
    GesturePreset Preset,
    GestureOptions Options,
    bool LeftButtonEnabled = false,
    bool MiddleButtonEnabled = false,
    bool XButton1Enabled = false,
    bool XButton2Enabled = false,
    bool RightButtonEnabled = true);
