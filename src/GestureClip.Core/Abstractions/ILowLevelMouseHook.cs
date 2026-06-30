using GestureClip.Core.Gestures;

namespace GestureClip.Core.Abstractions;

public interface ILowLevelMouseHook
{
    event EventHandler<MouseHookEventArgs>? MouseEventReceived;

    void Start();

    void Stop();
}
