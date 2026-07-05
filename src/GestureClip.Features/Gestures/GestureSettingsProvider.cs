using GestureClip.Core.Abstractions;
using GestureClip.Core.Settings;
using GestureClip.Core.Gestures;

namespace GestureClip.Features.Gestures;

public sealed class GestureSettingsProvider : IGestureSettingsProvider
{
    private GestureSettings _current;

    public GestureSettingsProvider(ISettingsService settingsService)
    {
        _current = new GestureSettings(
            settingsService.Get(SettingKeys.GestureEnabled, true),
            settingsService.Get(SettingKeys.GestureShowOverlay, true),
            settingsService.Get(SettingKeys.GestureCloseWindowEnabled, false),
            settingsService.Get(SettingKeys.GestureDebugEnabled, false),
            settingsService.Get(SettingKeys.GesturePreset, GesturePreset.EditEnhanced),
            new GestureOptions(
                settingsService.Get(SettingKeys.GestureTriggerThreshold, 20),
                settingsService.Get(SettingKeys.GestureSegmentThreshold, 16),
                settingsService.Get(SettingKeys.GestureMaxDurationMs, 2000),
                2),
            settingsService.Get(SettingKeys.GestureTriggerLeftButtonEnabled, false),
            settingsService.Get(SettingKeys.GestureTriggerMiddleButtonEnabled, false),
            settingsService.Get(SettingKeys.GestureTriggerXButton1Enabled, false),
            settingsService.Get(SettingKeys.GestureTriggerXButton2Enabled, false),
            settingsService.Get(SettingKeys.GestureTriggerRightButtonEnabled, true));
    }

    public GestureSettings GetCurrent()
    {
        return _current;
    }

    public void Update(GestureSettings settings)
    {
        _current = settings;
    }
}
