using GestureClip.Core.Gestures;

namespace GestureClip.Core.Abstractions;

public interface ILowLevelMouseHook
{
    event EventHandler<MouseHookEventArgs>? MouseEventReceived;

    Task StartAsync(CancellationToken cancellationToken);

    Task StopAsync(CancellationToken cancellationToken);
}
