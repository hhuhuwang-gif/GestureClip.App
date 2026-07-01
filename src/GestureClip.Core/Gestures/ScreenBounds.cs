namespace GestureClip.Core.Gestures;

public sealed record ScreenBounds(int Left, int Top, int Right, int Bottom)
{
    public int Width => Right - Left;

    public int Height => Bottom - Top;
}
