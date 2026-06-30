using GestureClip.Core.Gestures;

namespace GestureClip.Core.Abstractions;

public interface IMouseGestureRecognizer
{
    GestureResult Recognize(IReadOnlyList<GesturePoint> points, GestureOptions options);
}
