namespace GestureClip.Core.Abstractions;

using GestureClip.Core.Gestures;

public interface IMouseGestureService
{
    bool IsEnabled { get; }

    GestureDiagnosticsSnapshot Diagnostics { get; }

    Task StartAsync(CancellationToken cancellationToken);

    Task StopAsync(CancellationToken cancellationToken);
}
