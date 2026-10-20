namespace GestureClip.Core.Gestures;

public sealed record GestureAppProfile(
    string ProcessName,
    bool IsEnabled,
    IReadOnlyList<GestureAppBinding> Bindings);

public sealed record GestureAppBinding(
    string Pattern,
    bool IsLeftButtonModified,
    BuiltInGestureAction Action);
