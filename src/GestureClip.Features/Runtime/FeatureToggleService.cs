using GestureClip.Core.Abstractions;
using GestureClip.Core.Runtime;
using GestureClip.Core.Settings;
using GestureClip.Features.Gestures;
using Microsoft.Extensions.Logging;

namespace GestureClip.Features.Runtime;

public sealed class FeatureToggleService : IFeatureToggleService
{
    private readonly IClipboardService _clipboardService;
    private readonly IMouseGestureService _mouseGestureService;
    private readonly IEdgeTriggerService _edgeTriggerService;
    private readonly IGestureSettingsProvider _gestureSettingsProvider;
    private readonly ISettingsService _settingsService;
    private readonly ILogger<FeatureToggleService> _logger;

    public FeatureToggleService(
        IClipboardService clipboardService,
        IMouseGestureService mouseGestureService,
        IEdgeTriggerService edgeTriggerService,
        IGestureSettingsProvider gestureSettingsProvider,
        ISettingsService settingsService,
        ILogger<FeatureToggleService> logger)
    {
        _clipboardService = clipboardService;
        _mouseGestureService = mouseGestureService;
        _edgeTriggerService = edgeTriggerService;
        _gestureSettingsProvider = gestureSettingsProvider;
        _settingsService = settingsService;
        _logger = logger;
    }

    public FeatureToggleSnapshot GetSnapshot()
    {
        return new FeatureToggleSnapshot(_clipboardService.IsCaptureEnabled, _mouseGestureService.IsEnabled);
    }

    public Task ToggleClipboardCaptureAsync(CancellationToken cancellationToken)
    {
        return SetClipboardCaptureEnabledAsync(!_clipboardService.IsCaptureEnabled, cancellationToken);
    }

    public Task ToggleGestureAsync(CancellationToken cancellationToken)
    {
        return SetGestureEnabledAsync(!_mouseGestureService.IsEnabled, cancellationToken);
    }

    public Task SetClipboardCaptureEnabledAsync(bool enabled, CancellationToken cancellationToken)
    {
        return _clipboardService.SetCaptureEnabledAsync(enabled, cancellationToken);
    }

    public async Task SetGestureEnabledAsync(bool enabled, CancellationToken cancellationToken)
    {
        var current = _gestureSettingsProvider.GetCurrent();
        _gestureSettingsProvider.Update(current with { Enabled = enabled });
        await _settingsService.SetAsync(SettingKeys.GestureEnabled, enabled, cancellationToken);

        if (enabled)
        {
            await _mouseGestureService.StartAsync(cancellationToken);
            await _edgeTriggerService.StartAsync(cancellationToken);
        }
        else
        {
            await _mouseGestureService.StopAsync(cancellationToken);
            await _edgeTriggerService.StopAsync(cancellationToken);
        }

        _logger.LogInformation("Gesture state changed to {GestureEnabled}.", enabled);
    }
}
