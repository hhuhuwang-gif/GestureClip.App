using GestureClip.Core.Abstractions;
using GestureClip.Core.Gestures;
using GestureClip.Infrastructure.Win32;
using Microsoft.Extensions.Logging;

namespace GestureClip.Features.Gestures;

public sealed partial class MouseGestureService : IMouseGestureService
{
    private PendingGesture? HandleMove(MouseHookEvent mouseEvent)
    {
        if (_state == GestureRuntimeState.Idle || _startPoint is null || _activeSettings is null)
        {
            return null;
        }

        if (IsExpired(mouseEvent.Time, _activeSettings))
        {
            var clickButton = _activeTriggerButton;
            var clickPoint = _startPoint;
            ResetState("TimeoutReset");
            _synthesizeRightClickOnNextRightButtonUp = true;
            _pendingSyntheticClickButton = clickButton;
            _pendingSyntheticClickPoint = clickPoint;
            _ = _gestureOverlayService.HideAsync(CancellationToken.None);
            return null;
        }

        var movePoint = ToPoint(mouseEvent);
        var distanceSquaredFromStart = DistanceSquared(_startPoint, movePoint);
        var lastPoint = _points[^1];
        var segmentThresholdSquared = Square(_activeSettings.Options.SegmentThreshold);
        if (DistanceSquared(lastPoint, movePoint) >= segmentThresholdSquared)
        {
            _points.Add(movePoint);
            TrimTrackedPoints();
            _traceDirty = true;
        }

        if (_state == GestureRuntimeState.Tracking &&
            distanceSquaredFromStart >= Square(_activeSettings.Options.TriggerThreshold))
        {
            _state = GestureRuntimeState.GestureActive;
            LogDebug(_activeSettings, "GestureActive entered");
            if (_activeSettings.ShowOverlay)
            {
                var startHudInfo = _hudInfoProvider.GetInfo(_activeSettings.Preset, (string?)null);
                _ = _gestureOverlayService.ShowGestureStartAsync(_startPoint, startHudInfo, CancellationToken.None);
            }
        }

        RefreshMoveFeedback(mouseEvent, distanceSquaredFromStart);
        return null;
    }

    private void RefreshMoveFeedback(MouseHookEvent mouseEvent, double distanceSquaredFromStart)
    {
        if (_activeSettings!.DebugEnabled)
        {
            LogMoveDebug(
                _activeSettings,
                mouseEvent.X,
                mouseEvent.Y,
                Math.Sqrt(distanceSquaredFromStart),
                _state);
        }

        if (_activeSettings.ShowOverlay &&
            _state == GestureRuntimeState.GestureActive &&
            _traceDirty &&
            (mouseEvent.Time - _lastHudRefreshAt).TotalMilliseconds >= HudRefreshIntervalMs)
        {
            var preview = _recognizer.Recognize(_points, _activeSettings.Options);
            var previewPattern = NormalizePreviewPattern(preview.Pattern);
            var hudInfo = previewPattern is null
                ? _hudInfoProvider.GetInfo(_activeSettings.Preset, previewPattern)
                : _hudInfoProvider.GetInfo(_activeSettings.Preset, new GestureExecutionContext(previewPattern, _leftButtonModifier, _gestureTargetHwnd, _gestureTargetProcessName));
            _lastHudRefreshAt = mouseEvent.Time;
            _traceDirty = false;
            _ = _gestureOverlayService.UpdateGestureAsync([.. _points], hudInfo, CancellationToken.None);
        }

    }
}
