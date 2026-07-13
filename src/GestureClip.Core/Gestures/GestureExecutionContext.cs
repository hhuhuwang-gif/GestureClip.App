namespace GestureClip.Core.Gestures;

/// <param name="TargetWindowHandle">
/// Foreground window captured at gesture start (right-button-down). Used to re-focus before paste.
/// </param>
public sealed record GestureExecutionContext(
    string Pattern,
    bool IsLeftButtonModified,
    nint TargetWindowHandle = 0)
{
    public bool HasLeftButtonModifier => IsLeftButtonModified;
}
