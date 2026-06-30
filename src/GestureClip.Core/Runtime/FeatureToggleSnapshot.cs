namespace GestureClip.Core.Runtime;

public sealed record FeatureToggleSnapshot(
    bool ClipboardCaptureEnabled,
    bool GestureEnabled);
