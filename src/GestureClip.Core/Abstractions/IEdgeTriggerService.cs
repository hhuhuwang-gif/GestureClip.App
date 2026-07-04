namespace GestureClip.Core.Abstractions;

public interface IEdgeTriggerService
{
    bool IsEnabled { get; }

    GestureClip.Core.Gestures.EdgeTriggerDiagnosticsSnapshot Diagnostics { get; }

    Task StartAsync(CancellationToken cancellationToken);

    Task StopAsync(CancellationToken cancellationToken);

    void RefreshSettings();
}
