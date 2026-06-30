using GestureClip.Core.Runtime;

namespace GestureClip.Core.Abstractions;

public interface IFeatureToggleService
{
    FeatureToggleSnapshot GetSnapshot();

    Task SetClipboardCaptureEnabledAsync(bool enabled, CancellationToken cancellationToken);

    Task SetGestureEnabledAsync(bool enabled, CancellationToken cancellationToken);

    Task ToggleClipboardCaptureAsync(CancellationToken cancellationToken);

    Task ToggleGestureAsync(CancellationToken cancellationToken);
}
