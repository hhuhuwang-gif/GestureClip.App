using GestureClip.Core.Abstractions;
using GestureClip.Core.Gestures;
using GestureClip.Infrastructure.Win32;
using Microsoft.Extensions.Logging;

namespace GestureClip.Features.Gestures;

public sealed partial class MouseGestureService : IMouseGestureService
{
    private PendingGesture? HandleButtonUp(MouseHookEventArgs args, out PendingClick? pendingClick)
    {
        var mouseEvent = args.Event;
        pendingClick = null;
        var upButton = GetTriggerButton(mouseEvent.Type);
        LogDebug(_activeSettings, "{Button}Up: state={State}, pointCount={PointCount}", upButton, _state, _points.Count);
        if (_suppressNextButtonUp == upButton)
        {
            args.Suppress = true;
            _suppressNextButtonUp = null;
            LogDebug(_activeSettings, "Suppressed original {Button} button", upButton);
            return null;
        }

        if (TryHandleRockerButtonUp(upButton, args, out var rockerGesture))
        {
            return rockerGesture;
        }

        if (TryHandleLeftClickConfirmUp(upButton, args, mouseEvent, out var confirmedGesture))
        {
            return confirmedGesture;
        }

        if (_synthesizeRightClickOnNextRightButtonUp)
        {
            args.Suppress = true;
            _synthesizeRightClickOnNextRightButtonUp = false;
            var buttonToSynthesize = _pendingSyntheticClickButton;
            var clickPoint = _pendingSyntheticClickPoint ?? ToPoint(mouseEvent);
            _pendingSyntheticClickPoint = null;
            _pendingSyntheticClickButton = GestureTriggerButton.Right;
            pendingClick = new PendingClick(buttonToSynthesize, clickPoint.X, clickPoint.Y);
            return null;
        }

        if (_state == GestureRuntimeState.Idle)
        {
            return null;
        }

        if (upButton != _activeTriggerButton)
        {
            return null;
        }

        args.Suppress = true;
        LogDebug(_activeSettings, "Suppressed original {Button} button", upButton);

        return CompleteButtonRelease(mouseEvent, out pendingClick);
    }

    private PendingGesture? CompleteButtonRelease(MouseHookEvent mouseEvent, out PendingClick? pendingClick)
    {
        pendingClick = null;
        if (_wheelGestureFired)
        {
            // Wheel action(s) already executed during the hold; release ends the
            // gesture quietly — no pattern run, no synthetic click, no context menu.
            _wheelGestureFired = false;
            var wheelOverlayShown = _activeSettings?.ShowOverlay == true && _state == GestureRuntimeState.GestureActive;
            ResetState("WheelGestureReset");
            if (wheelOverlayShown)
            {
                _ = _gestureOverlayService.HideAsync(CancellationToken.None);
            }

            return null;
        }

        if (_activeSettings is null)
        {
            ResetState("StateReset");
            return null;
        }

        var upPoint = ToPoint(mouseEvent);
        if (DistanceSquared(_points[^1], upPoint) >= 1)
        {
            _points.Add(upPoint);
            TrimTrackedPoints();
        }

        if (_state != GestureRuntimeState.GestureActive || IsExpired(mouseEvent.Time, _activeSettings))
        {
            var expired = IsExpired(mouseEvent.Time, _activeSettings);
            var shouldHideOverlay = _state == GestureRuntimeState.GestureActive || expired;
            var clickPoint = _startPoint ?? upPoint;
            var clickButton = _activeTriggerButton;
            ResetState(expired ? "TimeoutReset" : "StateReset");
            if (shouldHideOverlay)
            {
                _ = _gestureOverlayService.HideAsync(CancellationToken.None);
            }

            pendingClick = new PendingClick(clickButton, clickPoint.X, clickPoint.Y);
            return null;
        }

        return CreateReleasedGesture();
    }

    private PendingGesture CreateReleasedGesture()
    {
        var isLeftButtonModified = _leftButtonModifier;
        if (_leftClickConfirmDown)
        {
            _suppressNextButtonUp = GestureTriggerButton.Left;
        }

        var pendingGesture = new PendingGesture(
            [.. _points],
            _activeSettings!.Options,
            _activeSettings.Preset,
            _activeSettings.ShowOverlay,
            isLeftButtonModified,
            PatternOverride: null,
            TargetWindowHandle: _gestureTargetHwnd,
            TargetProcessName: _gestureTargetProcessName);
        ResetState("StateReset");
        return pendingGesture;
    }
}
