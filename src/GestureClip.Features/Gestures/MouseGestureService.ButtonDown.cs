using GestureClip.Core.Abstractions;
using GestureClip.Core.Gestures;
using GestureClip.Infrastructure.Win32;
using Microsoft.Extensions.Logging;

namespace GestureClip.Features.Gestures;

public sealed partial class MouseGestureService : IMouseGestureService
{
    private PendingGesture? HandleButtonDown(MouseHookEventArgs args)
    {
        var mouseEvent = args.Event;
        var triggerButton = GetTriggerButton(mouseEvent.Type);
        if (TryHandleLeftClickConfirmDown(triggerButton, args))
        {
            return null;
        }

        if (TryHandleRockerButtonDown(triggerButton, args))
        {
            return null;
        }

        _synthesizeRightClickOnNextRightButtonUp = false;
        _activeSettings = _settingsProvider.GetCurrent();
        IsEnabled = _activeSettings.Enabled;
        if (!IsEnabled || !IsTriggerEnabled(triggerButton, _activeSettings))
        {
            LogDebug(_activeSettings, "Gesture disabled by setting");
            ResetState("StateReset");
            return null;
        }

        var foreground = _foregroundAppService.GetCurrent();
        if (_appBlacklistService.IsGestureBlockedCached(foreground.ProcessName))
        {
            LogDebug(_activeSettings, "Gesture skipped: foreground process is blacklisted.");
            ResetState("StateReset");
            return null;
        }

        _state = GestureRuntimeState.Tracking;
        _activeTriggerButton = triggerButton;
        _startPoint = ToPoint(mouseEvent);
        _gestureStartedAt = mouseEvent.Time;
        _points = [_startPoint];
        // Capture focus target immediately — HUD/overlay must not steal paste destination.
        _gestureTargetHwnd = WindowNativeMethods.GetForegroundWindow();
        _gestureTargetProcessName = foreground.ProcessName;
        args.Suppress = true;
        LogDebug(_activeSettings, "{Button}Down: x={X}, y={Y}", triggerButton, mouseEvent.X, mouseEvent.Y);
        LogDebug(_activeSettings, "Suppressed original {Button} button", triggerButton);
        return null;
    }
}
