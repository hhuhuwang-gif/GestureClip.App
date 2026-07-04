namespace GestureClip.Core.Gestures;

public sealed record GestureHudInfo(
    string DirectionText,
    string Pattern,
    string ActionName,
    string ShortcutText,
    string PresetName)
{
    public BuiltInGestureAction Action { get; init; } = BuiltInGestureAction.None;
}
