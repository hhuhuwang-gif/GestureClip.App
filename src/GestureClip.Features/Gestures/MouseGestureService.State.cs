using GestureClip.Core.Abstractions;
using GestureClip.Core.Gestures;
using GestureClip.Infrastructure.Win32;
using Microsoft.Extensions.Logging;

namespace GestureClip.Features.Gestures;

public sealed partial class MouseGestureService : IMouseGestureService
{
    private void RecordPostGestureStatsInBackground(BuiltInGestureAction action)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await _workstationDashboardService.RecordGestureAsync(DateTimeOffset.UtcNow, CancellationToken.None);
                await RecordWorkerLevelAsync(action);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Gesture stats recording failed.");
            }
        });
    }

    private async Task RecordWorkerLevelAsync(BuiltInGestureAction action)
    {
        try
        {
            var snapshot = await _workerLevelService.RecordActionAsync(action, true, DateTimeOffset.UtcNow, CancellationToken.None);
            if (snapshot.LeveledUp)
            {
                await _workerLevelUpService.ShowLevelUpAsync(snapshot, CancellationToken.None);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Worker level recording failed; gesture action already completed.");
        }
    }
    private void ResetState(string reason)
    {
        if (reason != "StateReset" ||
            _state != GestureRuntimeState.Idle ||
            _startPoint is not null ||
            _activeSettings is not null ||
            _synthesizeRightClickOnNextRightButtonUp)
        {
            _logger.LogDebug("GestureRecovery {Reason}", reason);
        }

        _state = GestureRuntimeState.Idle;
        _activeSettings = null;
        _points = [];
        _startPoint = null;
        _gestureStartedAt = null;
        _synthesizeRightClickOnNextRightButtonUp = false;
        _pendingSyntheticClickButton = GestureTriggerButton.Right;
        _pendingSyntheticClickPoint = null;
        _activeTriggerButton = GestureTriggerButton.Right;
        _rightLeftRockerDown = false;
        _wheelGestureFired = false;
        _leftClickConfirmDown = false;
        _leftButtonModifier = false;
        _lastHudRefreshAt = DateTimeOffset.MinValue;
        _traceDirty = false;
        // Keep _gestureTargetHwnd until pending gesture is built; cleared after copy into PendingGesture via reset after return.
        // Intentionally NOT cleared here when called after PendingGesture construction — value already copied.
        _gestureTargetHwnd = 0;
        _gestureTargetProcessName = null;
    }

    private bool TryHandleLeftClickConfirmDown(GestureTriggerButton triggerButton, MouseHookEventArgs args)
    {
        if (triggerButton != GestureTriggerButton.Left ||
            _state != GestureRuntimeState.GestureActive ||
            _activeTriggerButton != GestureTriggerButton.Right ||
            _activeSettings is null)
        {
            return false;
        }

        _leftButtonModifier = true;
        _leftClickConfirmDown = true;
        args.Suppress = true;
        LogDebug(_activeSettings, "Suppressed original Left button for gesture click confirm");
        return true;
    }

    private bool TryHandleLeftClickConfirmUp(
        GestureTriggerButton upButton,
        MouseHookEventArgs args,
        MouseHookEvent mouseEvent,
        out PendingGesture? pendingGesture)
    {
        pendingGesture = null;
        if (upButton != GestureTriggerButton.Left ||
            !_leftClickConfirmDown ||
            _state != GestureRuntimeState.GestureActive ||
            _activeTriggerButton != GestureTriggerButton.Right ||
            _activeSettings is null)
        {
            return false;
        }

        args.Suppress = true;
        LogDebug(_activeSettings, "Suppressed original Left button for gesture click confirm");
        var upPoint = ToPoint(mouseEvent);
        if (_points.Count > 0 && DistanceSquared(_points[^1], upPoint) >= 1)
        {
            _points.Add(upPoint);
            TrimTrackedPoints();
        }

        pendingGesture = new PendingGesture(
            [.. _points],
            _activeSettings.Options,
            _activeSettings.Preset,
            _activeSettings.ShowOverlay,
            IsLeftButtonModified: true,
            PatternOverride: null,
            TargetWindowHandle: _gestureTargetHwnd,
                    TargetProcessName: _gestureTargetProcessName);
        _suppressNextButtonUp = GestureTriggerButton.Right;
        ResetState("StateReset");
        return true;
    }

    private bool TryHandleRockerButtonDown(GestureTriggerButton triggerButton, MouseHookEventArgs args)
    {
        if (triggerButton != GestureTriggerButton.Left ||
            _state != GestureRuntimeState.Tracking ||
            _activeTriggerButton != GestureTriggerButton.Right ||
            _activeSettings is null)
        {
            return false;
        }

        _rightLeftRockerDown = true;
        args.Suppress = true;
        LogDebug(_activeSettings, "Suppressed original Left button for R+L rocker gesture");
        return true;
    }

    private bool TryHandleRockerButtonUp(
        GestureTriggerButton upButton,
        MouseHookEventArgs args,
        out PendingGesture? pendingGesture)
    {
        pendingGesture = null;
        if ((upButton != GestureTriggerButton.Left && upButton != GestureTriggerButton.Right) ||
            !_rightLeftRockerDown ||
            _activeTriggerButton != GestureTriggerButton.Right ||
            _activeSettings is null ||
            _startPoint is null)
        {
            return false;
        }

        args.Suppress = true;
        LogDebug(_activeSettings, "Suppressed original {Button} button for R+L rocker gesture", upButton);
        _suppressNextButtonUp = upButton == GestureTriggerButton.Left
            ? GestureTriggerButton.Right
            : GestureTriggerButton.Left;
        pendingGesture = new PendingGesture(
            [_startPoint],
            _activeSettings.Options,
            _activeSettings.Preset,
            _activeSettings.ShowOverlay,
            IsLeftButtonModified: false,
            PatternOverride: "R+L",
            TargetWindowHandle: _gestureTargetHwnd,
                    TargetProcessName: _gestureTargetProcessName);
        ResetState("StateReset");
        return true;
    }
}
