namespace GestureClip.Core.Abstractions;

public interface IEdgeTriggerService
{
    bool IsEnabled { get; }

    Task StartAsync(CancellationToken cancellationToken);

    Task StopAsync(CancellationToken cancellationToken);
}
