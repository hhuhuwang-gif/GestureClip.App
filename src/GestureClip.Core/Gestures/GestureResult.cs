namespace GestureClip.Core.Gestures;

public sealed record GestureResult(string? Pattern, bool IsValid)
{
    public static GestureResult Invalid(string? pattern = null) => new(pattern, false);
}
