using GestureClip.Core.Abstractions;
using GestureClip.Core.Gestures;
using GestureClip.Infrastructure.Win32;
using Microsoft.Extensions.Logging;

namespace GestureClip.Features.Gestures;

public sealed partial class MouseGestureService : IMouseGestureService
{
    private void OnMouseEventReceived(object? sender, MouseHookEventArgs args)
    {
        try
        {
            PendingGesture? pendingGesture;
            PendingClick? pendingClick;

            lock (_syncRoot)
            {
                pendingGesture = HandleMouseEvent(args, out pendingClick);
            }

            if (pendingClick is not null)
            {
                _ = Task.Run(() => SynthesizeClick(pendingClick.Button, pendingClick.X, pendingClick.Y));
            }

            if (pendingGesture is not null)
            {
                _ = Task.Run(() => RecognizeAndExecuteGestureAsync(pendingGesture));
            }
        }
        catch (Exception ex)
        {
            lock (_syncRoot)
            {
                SetLastError(ex.Message);
                ResetState("ExceptionReset");
            }

            _ = _gestureOverlayService.HideAsync(CancellationToken.None);
            _logger.LogError(ex, "Mouse gesture event handling failed.");
        }
    }

    private PendingGesture? HandleMouseEvent(MouseHookEventArgs args, out PendingClick? pendingClick)
    {
        pendingClick = null;
        if (!IsEnabled)
        {
            return null;
        }

        var mouseEvent = args.Event;

        if (mouseEvent.IsInjected && DateTimeOffset.UtcNow < _ignoreInjectedUntil)
        {
            // Do not Suppress here: intentional SynthesizeClick must reach the app.
            _logger.LogDebug("GestureRecovery InjectedMouseClickIgnored");
            return null;
        }

        // Post-gesture guard (Idle only): swallow residual RIGHT UP that would open context menu.
        // Must not apply while Tracking/GestureActive — that would kill the next gesture's button-up.
        if (mouseEvent.Type == MouseHookEventType.RightButtonUp &&
            _state == GestureRuntimeState.Idle &&
            DateTimeOffset.UtcNow < _suppressRightButtonUntil)
        {
            args.Suppress = true;
            _logger.LogDebug("Post-gesture residual right-up suppressed to prevent context menu.");
            return null;
        }

        _lastEventAt = mouseEvent.Time;

        if (IsSafetyTimeoutExceeded(mouseEvent.Time))
        {
            var clickButton = _activeTriggerButton;
            var clickPoint = _startPoint;
            ResetState("TimeoutReset");
            _synthesizeRightClickOnNextRightButtonUp = true;
            _pendingSyntheticClickButton = clickButton;
            _pendingSyntheticClickPoint = clickPoint;
            _ = _gestureOverlayService.HideAsync(CancellationToken.None);
        }

        return mouseEvent.Type switch
        {
            MouseHookEventType.LeftButtonDown or MouseHookEventType.RightButtonDown or
                MouseHookEventType.MiddleButtonDown or MouseHookEventType.XButton1Down or
                MouseHookEventType.XButton2Down => HandleButtonDown(args),
            MouseHookEventType.Move => HandleMove(mouseEvent),
            MouseHookEventType.Wheel => HandleWheel(args),
            MouseHookEventType.LeftButtonUp or MouseHookEventType.RightButtonUp or
                MouseHookEventType.MiddleButtonUp or MouseHookEventType.XButton1Up or
                MouseHookEventType.XButton2Up => HandleButtonUp(args, out pendingClick),
            _ => null
        };
    }
}
