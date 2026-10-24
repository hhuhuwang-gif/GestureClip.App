using GestureClip.Core.Gestures;

namespace GestureClip.Core.Abstractions;

public interface IMouseButtonStateProvider
{
    bool IsPressed(GestureTriggerButton button);
}
