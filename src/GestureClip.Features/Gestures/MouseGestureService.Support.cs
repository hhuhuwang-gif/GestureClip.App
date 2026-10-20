using GestureClip.Core.Abstractions;
using GestureClip.Core.Gestures;
using GestureClip.Infrastructure.Win32;
using Microsoft.Extensions.Logging;

namespace GestureClip.Features.Gestures;

public sealed partial class MouseGestureService : IMouseGestureService
{
    private void TrimTrackedPoints()
    {
        if (_points.Count <= MaxTrackedPoints)
        {
            return;
        }

        var overflow = _points.Count - MaxTrackedPoints;
        _points.RemoveRange(0, overflow);
    }

    private static string? NormalizePreviewPattern(string? pattern)
    {
        return pattern is { Length: > MaxPreviewPatternLength } ? null : pattern;
    }

    private void SynthesizeClick(GestureTriggerButton button, int x, int y)
    {
        try
        {
            lock (_syncRoot)
            {
                // Allow this intentional click through (clear post-gesture right suppress).
                _suppressRightButtonUntil = DateTimeOffset.MinValue;
                _ignoreInjectedUntil = DateTimeOffset.UtcNow.AddMilliseconds(500);
            }

            _rightClickSynthesizer.SynthesizeClick(button, x, y);
            LogDebug(_settingsProvider.GetCurrent(), "Synthesized normal {Button} click", button);
        }
        catch (Exception ex)
        {
            lock (_syncRoot)
            {
                SetLastError(ex.Message);
            }

            _logger.LogError(ex, "Failed to synthesize mouse click.");
        }

    }

    private static bool IsPasteFamilyAction(BuiltInGestureAction action) => action is
        BuiltInGestureAction.Paste or
        BuiltInGestureAction.SmartPaste or
        BuiltInGestureAction.PasteAndEnter or
        BuiltInGestureAction.PastePlainText or
        BuiltInGestureAction.PasteLatestClipboardItem;

    private static bool IsRightButtonEvent(MouseHookEventType type) =>
        type is MouseHookEventType.RightButtonDown or MouseHookEventType.RightButtonUp;

    private void SetHookStatus(string status)
    {
        lock (_syncRoot)
        {
            _hookStatus = status;
        }
    }

    private void SetLastError(string? error)
    {
        _lastError = error;
    }

    private void LogMoveDebug(GestureSettings settings, int x, int y, double distanceFromStart, GestureRuntimeState state)
    {
        if (!settings.DebugEnabled)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        if ((now - _lastMoveDebugLogAt).TotalMilliseconds < 100)
        {
            return;
        }

        _lastMoveDebugLogAt = now;
        _logger.LogInformation(
            "Move: x={X}, y={Y}, distanceFromStart={DistanceFromStart:F1}, state={State}",
            x,
            y,
            distanceFromStart,
            state);
    }

    private void LogDebug(GestureSettings? settings, string message, params object?[] args)
    {
        if (settings?.DebugEnabled == true)
        {
            _logger.LogInformation(message, args);
        }
    }

    private bool IsExpired(DateTimeOffset currentTime, GestureSettings settings)
    {
        return _gestureStartedAt is not null &&
            (currentTime - _gestureStartedAt.Value).TotalMilliseconds > settings.Options.MaxDurationMs;
    }

    private bool IsSafetyTimeoutExceeded(DateTimeOffset currentTime)
    {
        if (_state == GestureRuntimeState.Idle || _activeSettings is null || _gestureStartedAt is null)
        {
            return false;
        }

        return (currentTime - _gestureStartedAt.Value).TotalMilliseconds >
            _activeSettings.Options.MaxDurationMs + 500;
    }

    private static GestureTriggerButton GetTriggerButton(MouseHookEventType type)
    {
        return type switch
        {
            MouseHookEventType.LeftButtonDown or MouseHookEventType.LeftButtonUp => GestureTriggerButton.Left,
            MouseHookEventType.MiddleButtonDown or MouseHookEventType.MiddleButtonUp => GestureTriggerButton.Middle,
            MouseHookEventType.XButton1Down or MouseHookEventType.XButton1Up => GestureTriggerButton.XButton1,
            MouseHookEventType.XButton2Down or MouseHookEventType.XButton2Up => GestureTriggerButton.XButton2,
            _ => GestureTriggerButton.Right
        };
    }

    private static bool IsTriggerEnabled(GestureTriggerButton button, GestureSettings settings)
    {
        return button switch
        {
            GestureTriggerButton.Left => false,
            GestureTriggerButton.Right => settings.RightButtonEnabled,
            GestureTriggerButton.Middle => settings.MiddleButtonEnabled,
            GestureTriggerButton.XButton1 => settings.XButton1Enabled,
            GestureTriggerButton.XButton2 => settings.XButton2Enabled,
            _ => false
        };
    }

    private static bool IsDisabledByEnvironment()
    {
        return string.Equals(
            Environment.GetEnvironmentVariable("GESTURECLIP_DISABLE_GESTURES"),
            "1",
            StringComparison.Ordinal);
    }

    private static GesturePoint ToPoint(MouseHookEvent mouseEvent)
    {
        return new GesturePoint(mouseEvent.X, mouseEvent.Y, mouseEvent.Time);
    }

    private static double DistanceSquared(GesturePoint first, GesturePoint second)
    {
        var dx = (double)second.X - first.X;
        var dy = (double)second.Y - first.Y;
        return (dx * dx) + (dy * dy);
    }

    private static double Square(int value) => (double)value * value;

    private sealed record PendingClick(GestureTriggerButton Button, int X, int Y);

    private sealed record PendingGesture(
        IReadOnlyList<GesturePoint> Points,
        GestureOptions Options,
        GesturePreset Preset,
        bool ShowOverlay,
        bool IsLeftButtonModified = false,
        string? PatternOverride = null,
        nint TargetWindowHandle = 0,
        string? TargetProcessName = null);
}
