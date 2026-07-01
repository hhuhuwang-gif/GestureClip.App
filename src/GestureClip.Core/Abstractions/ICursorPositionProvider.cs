namespace GestureClip.Core.Abstractions;

using GestureClip.Core.Gestures;

public interface ICursorPositionProvider
{
    CursorPosition GetCurrentPosition();

    ScreenBounds GetVirtualScreenBounds();
}
