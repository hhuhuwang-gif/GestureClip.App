using GestureClip.Core.Gestures;
using GestureClip.Core.Settings;

namespace GestureClip.Features.Gestures;

public sealed partial class EdgeTriggerService
{
    private EdgeTriggerSettings LoadSettings()
    {
        return new EdgeTriggerSettings(
            _settingsService.Get(SettingKeys.EdgeTriggerEnabled, true),
            Math.Clamp(_settingsService.Get(SettingKeys.EdgeTriggerHotZoneSize, 8), 2, 64),
            Math.Clamp(_settingsService.Get(SettingKeys.EdgeTriggerDwellMs, 160), 50, 2000),
            Math.Clamp(_settingsService.Get(SettingKeys.EdgeTriggerCooldownMs, 450), 150, 5000),
            Math.Clamp(_settingsService.Get(SettingKeys.EdgeTriggerSlideThreshold, 56), 24, 400),
            _settingsService.Get(SettingKeys.EdgeTriggerTopLeftAction, BuiltInGestureAction.StartMenu),
            _settingsService.Get(SettingKeys.EdgeTriggerTopRightAction, BuiltInGestureAction.TaskSwitcher),
            _settingsService.Get(SettingKeys.EdgeTriggerBottomRightAction, BuiltInGestureAction.ShowDesktop),
            _settingsService.Get(SettingKeys.EdgeTriggerBottomLeftAction, BuiltInGestureAction.SwitchApp),
            _settingsService.Get(SettingKeys.EdgeTriggerLeftEdgeLeftButtonEnabled, false),
            _settingsService.Get(SettingKeys.EdgeTriggerLeftEdgeLeftButtonAction, BuiltInGestureAction.StartMenu),
            _settingsService.Get(SettingKeys.EdgeTriggerLeftEdgeMiddleButtonEnabled, true),
            _settingsService.Get(SettingKeys.EdgeTriggerLeftEdgeMiddleButtonAction, BuiltInGestureAction.ShowDesktop),
            _settingsService.Get(SettingKeys.EdgeTriggerLeftEdgeXButton1Enabled, true),
            _settingsService.Get(SettingKeys.EdgeTriggerLeftEdgeXButton1Action, BuiltInGestureAction.SwitchApp),
            _settingsService.Get(SettingKeys.EdgeTriggerLeftEdgeXButton2Enabled, true),
            _settingsService.Get(SettingKeys.EdgeTriggerLeftEdgeXButton2Action, BuiltInGestureAction.TaskSwitcher),
            _settingsService.Get(SettingKeys.EdgeTriggerTopRightWheelEnabled, true),
            _settingsService.Get(SettingKeys.EdgeTriggerTopRightWheelAction, BuiltInGestureAction.TaskSwitcher),
            _settingsService.Get(SettingKeys.EdgeTriggerSlideLeftEnabled, true),
            _settingsService.Get(SettingKeys.EdgeTriggerSlideLeftAction, BuiltInGestureAction.SwitchApp),
            _settingsService.Get(SettingKeys.EdgeTriggerSlideRightEnabled, true),
            _settingsService.Get(SettingKeys.EdgeTriggerSlideRightAction, BuiltInGestureAction.TaskSwitcher),
            _settingsService.Get(SettingKeys.EdgeTriggerSlideTopEnabled, true),
            _settingsService.Get(SettingKeys.EdgeTriggerSlideTopAction, BuiltInGestureAction.StartMenu),
            _settingsService.Get(SettingKeys.EdgeTriggerSlideBottomEnabled, true),
            _settingsService.Get(SettingKeys.EdgeTriggerSlideBottomAction, BuiltInGestureAction.PasteAndEnter));
    }

    private void ResetTracking()
    {
        _activeCorner = null;
        _enteredCornerAt = null;
        ResetSlideTracking();
    }

    private void ResetSlideTracking()
    {
        _activeSlideEdge = null;
        _slideStartPoint = null;
    }

    private void SetDiagnostics(
        string source,
        string position,
        BuiltInGestureAction action,
        string reason,
        DateTimeOffset? eventAt)
    {
        DateTimeOffset? cooldownUntil = _cooldownUntil > DateTimeOffset.UtcNow ? _cooldownUntil : null;
        _diagnostics = new EdgeTriggerDiagnosticsSnapshot(
            IsEnabled,
            source,
            position,
            action,
            reason,
            eventAt,
            cooldownUntil);
    }
}
