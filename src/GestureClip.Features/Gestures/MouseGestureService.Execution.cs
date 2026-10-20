using GestureClip.Core.Abstractions;
using GestureClip.Core.Gestures;
using GestureClip.Infrastructure.Win32;
using Microsoft.Extensions.Logging;

namespace GestureClip.Features.Gestures;

public sealed partial class MouseGestureService : IMouseGestureService
{
    private async Task RecognizeAndExecuteGestureAsync(PendingGesture pendingGesture)
    {
        try
        {
            var pattern = pendingGesture.PatternOverride;
            var isValid = !string.IsNullOrWhiteSpace(pattern);
            if (!isValid)
            {
                var result = _recognizer.Recognize(pendingGesture.Points, pendingGesture.Options);
                pattern = result.Pattern;
                isValid = result.IsValid && result.Pattern is not null;
            }

            if (isValid && pattern is not null)
            {
                await CompleteRecognizedGestureAsync(pendingGesture, pattern);
                return;
            }

            if (pendingGesture.ShowOverlay)
            {
                await _gestureOverlayService.HideAsync(CancellationToken.None);
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
            _logger.LogError(ex, "Gesture recognition failed.");
        }
    }

    private async Task CompleteRecognizedGestureAsync(PendingGesture pendingGesture, string pattern)
    {
        lock (_syncRoot) { _lastPattern = pattern; }
        _logger.LogDebug("Recognized Pattern: {Pattern} TargetHwnd={TargetHwnd}", pattern, pendingGesture.TargetWindowHandle);
        var context = new GestureExecutionContext(pattern, pendingGesture.IsLeftButtonModified,
            pendingGesture.TargetWindowHandle, pendingGesture.TargetProcessName);
        var action = _presetProvider.GetAction(pendingGesture.Preset, context);
        if (pendingGesture.ShowOverlay)
        {
            await _gestureOverlayService.CompleteGestureAsync(
                _hudInfoProvider.GetInfo(pendingGesture.Preset, context), CancellationToken.None);
            if (IsPasteFamilyAction(action))
            {
                await _gestureOverlayService.HideAsync(CancellationToken.None);
            }
        }
        lock (_syncRoot) { _suppressRightButtonUntil = DateTimeOffset.UtcNow.AddMilliseconds(450); }
        await ExecuteGestureAsync(context);
        if (pendingGesture.ShowOverlay && !IsPasteFamilyAction(action) && action != BuiltInGestureAction.None)
            await _gestureOverlayService.HideAsync(CancellationToken.None);
    }

    private async Task ExecuteGestureAsync(GestureExecutionContext context)
    {
        try
        {
            var settings = _settingsProvider.GetCurrent();
            var action = _presetProvider.GetAction(settings.Preset, context);
            lock (_syncRoot)
            {
                _lastAction = action;
            }

            LogDebug(settings, "Resolved Action: {Action}", action);
            if (action == BuiltInGestureAction.None)
            {
                return;
            }

            if (action == BuiltInGestureAction.CloseForegroundWindow && !settings.CloseWindowEnabled)
            {
                _logger.LogInformation("Close window gesture ignored because it is disabled.");
                return;
            }

            if (IsPasteFamilyAction(action))
            {
                PrepareGesturePaste();
            }

            await _actionExecutor.ExecuteAsync(action, context, CancellationToken.None);
            RecordPostGestureStatsInBackground(action);
            LogDebug(settings, "Action Executed: {Action}", action);
        }
        catch (Exception ex)
        {
            lock (_syncRoot)
            {
                SetLastError(ex.Message);
                ResetState("ExceptionReset");
            }

            _ = _gestureOverlayService.HideAsync(CancellationToken.None);
            _logger.LogError(ex, "Gesture action execution failed.");
        }
    }

    private void PrepareGesturePaste()
    {
        lock (_syncRoot) { _suppressRightButtonUntil = DateTimeOffset.UtcNow.AddMilliseconds(500); }
    }
}
