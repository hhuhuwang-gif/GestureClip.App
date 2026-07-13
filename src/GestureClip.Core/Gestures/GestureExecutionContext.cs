namespace GestureClip.Core.Gestures;

public sealed record GestureExecutionContext(string Pattern, bool IsLeftButtonModified)
{
    public bool HasLeftButtonModifier => IsLeftButtonModified;
}
