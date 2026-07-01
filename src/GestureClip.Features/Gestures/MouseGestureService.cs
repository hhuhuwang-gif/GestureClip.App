using GestureClip.Core.Abstractions;
using GestureClip.Core.Gestures;
using Microsoft.Extensions.Logging;

namespace GestureClip.Features.Gestures;

public sealed class MouseGestureService : IMouseGestureService
{
    private const int MaxTrackedPoints = 96;
    private const int MaxPreviewPatternLength = 2;

    private readonly ILowLevelMouseHook _mouseHook;
    private readonly IMouseGestureRecognizer _recognizer;
    private readonly IMouseGestureActionExecutor _actionExecutor;
    private readonly IRightClickSynthesizer _rightClickSynthesizer;
    private readonly IGestureSettingsProvider _settingsProvider;
    private readonly IGesturePresetProvider _presetProvider;
    private readonly IGestureHudInfoProvider _hudInfoProvider;
    private readonly IGestureOverlayService _gestureOverlayService;
    private readonly IForegroundAppService _foregroundAppService;
    private readonly IAppBlacklistService _appBlacklistService;
    private readonly ILogger<MouseGestureService> _logger;
    private readonly object _syncRoot = new();
    private int _started;
    private GestureRuntimeState _state = GestureRuntimeState.Idle;
    private GestureSettings? _activeSettings;
    private List<GesturePoint> _points = [];
    private GesturePoint? _startPoint;
    private DateTimeOffset? _gestureStartedAt;
    private DateTimeOffset _ignoreInjectedUntil = DateTimeOffset.MinValue;
    private DateTimeOffset _lastMoveDebugLogAt = DateTimeOffset.MinValue;
    private bool _synthesizeRightClickOnNextRightButtonUp;
    private string _hookStatus = "未安装";
    private string? _lastPattern;
    private BuiltInGestureAction _lastAction = BuiltInGestureAction.None;
    private string? _lastError;
    private DateTimeOffset? _lastEventAt;

    public MouseGestureService(
        ILowLevelMouseHook mouseHook,
        IMouseGestureRecognizer recognizer,
        IMouseGestureActionExecutor actionExecutor,
        IRightClickSynthesizer rightClickSynthesizer,
        IGestureSettingsProvider settingsProvider,
        IGesturePresetProvider presetProvider,
        IGestureHudInfoProvider hudInfoProvider,
        IGestureOverlayService gestureOverlayService,
        IForegroundAppService foregroundAppService,
        IAppBlacklistService appBlacklistService,
        ILogger<MouseGestureService> logger)
    {
        _mouseHook = mouseHook;
        _recognizer = recognizer;
        _actionExecutor = actionExecutor;
        _rightClickSynthesizer = rightClickSynthesizer;
        _settingsProvider = settingsProvider;
        _presetProvider = presetProvider;
        _hudInfoProvider = hudInfoProvider;
        _gestureOverlayService = gestureOverlayService;
        _foregroundAppService = foregroundAppService;
        _appBlacklistService = appBlacklistService;
        _logger = logger;
        IsEnabled = !IsDisabledByEnvironment() && _settingsProvider.GetCurrent().Enabled;
    }

    public bool IsEnabled { get; private set; }

    public GestureDiagnosticsSnapshot Diagnostics
    {
        get
        {
            lock (_syncRoot)
            {
                return new GestureDiagnosticsSnapshot(
                    _hookStatus,
                    _state,
                    _lastPattern,
                    _lastAction,
                    _lastError,
                    _lastEventAt,
                    IsDisabledByEnvironment());
            }
        }
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (IsDisabledByEnvironment())
        {
            IsEnabled = false;
            SetHookStatus("被环境变量禁用");
            _logger.LogInformation("Mouse gesture disabled by environment variable");
            return Task.CompletedTask;
        }

        var settings = _settingsProvider.GetCurrent();
        IsEnabled = settings.Enabled;
        if (!IsEnabled)
        {
            SetHookStatus("未安装");
            LogDebug(settings, "Gesture disabled by setting");
            return Task.CompletedTask;
        }

        if (Interlocked.Exchange(ref _started, 1) == 1)
        {
            return Task.CompletedTask;
        }

        _mouseHook.MouseEventReceived += OnMouseEventReceived;

        try
        {
            _mouseHook.Start();
            SetHookStatus("已安装");
            LogDebug(settings, "Hook installed");
        }
        catch (Exception ex)
        {
            _mouseHook.MouseEventReceived -= OnMouseEventReceived;
            Interlocked.Exchange(ref _started, 0);
            SetHookStatus("安装失败");
            SetLastError(ex.Message);
            _logger.LogError(ex, "Hook failed");
            throw;
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        if (Interlocked.Exchange(ref _started, 0) == 0)
        {
            return Task.CompletedTask;
        }

        _mouseHook.MouseEventReceived -= OnMouseEventReceived;
        _mouseHook.Stop();

        lock (_syncRoot)
        {
            ResetState("StateReset");
            _hookStatus = "未安装";
        }

        return Task.CompletedTask;
    }

    private void OnMouseEventReceived(object? sender, MouseHookEventArgs args)
    {
        try
        {
            PendingGesture? pendingGesture = null;

            lock (_syncRoot)
            {
                pendingGesture = HandleMouseEvent(args);
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

            _logger.LogError(ex, "Mouse gesture event handling failed.");
        }
    }

    private PendingGesture? HandleMouseEvent(MouseHookEventArgs args)
    {
        if (!IsEnabled)
        {
            return null;
        }

        var mouseEvent = args.Event;
        if (mouseEvent.IsInjected && DateTimeOffset.UtcNow < _ignoreInjectedUntil)
        {
            _logger.LogDebug("GestureRecovery InjectedRightClickIgnored");
            return null;
        }

        _lastEventAt = mouseEvent.Time;

        if (IsSafetyTimeoutExceeded(mouseEvent.Time))
        {
            ResetState("TimeoutReset");
            _synthesizeRightClickOnNextRightButtonUp = true;
            _ = _gestureOverlayService.HideAsync(CancellationToken.None);
        }

        switch (mouseEvent.Type)
        {
            case MouseHookEventType.RightButtonDown:
                _synthesizeRightClickOnNextRightButtonUp = false;
                _activeSettings = _settingsProvider.GetCurrent();
                IsEnabled = _activeSettings.Enabled;
                if (!IsEnabled)
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
                _startPoint = ToPoint(mouseEvent);
                _gestureStartedAt = mouseEvent.Time;
                _points = [_startPoint];
                args.Suppress = true;
                LogDebug(_activeSettings, "RightButtonDown: x={X}, y={Y}", mouseEvent.X, mouseEvent.Y);
                LogDebug(_activeSettings, "Suppressed original right button");
                if (_activeSettings.ShowOverlay)
                {
                    var hudInfo = _hudInfoProvider.GetInfo(_activeSettings.Preset, null);
                    _ = _gestureOverlayService.ShowGestureStartAsync(_startPoint, hudInfo, CancellationToken.None);
                }

                return null;

            case MouseHookEventType.Move:
                if (_state == GestureRuntimeState.Idle || _startPoint is null || _activeSettings is null)
                {
                    return null;
                }

                if (IsExpired(mouseEvent.Time, _activeSettings))
                {
                    ResetState("TimeoutReset");
                    _synthesizeRightClickOnNextRightButtonUp = true;
                    _ = _gestureOverlayService.HideAsync(CancellationToken.None);
                    return null;
                }

                var movePoint = ToPoint(mouseEvent);
                var distanceFromStart = Distance(_startPoint, movePoint);
                var lastPoint = _points[^1];
                if (Distance(lastPoint, movePoint) >= _activeSettings.Options.SegmentThreshold)
                {
                    _points.Add(movePoint);
                    TrimTrackedPoints();
                }

                if (_state == GestureRuntimeState.Tracking &&
                    distanceFromStart >= _activeSettings.Options.TriggerThreshold)
                {
                    _state = GestureRuntimeState.GestureActive;
                    LogDebug(_activeSettings, "GestureActive entered");
                }

                LogMoveDebug(_activeSettings, mouseEvent.X, mouseEvent.Y, distanceFromStart, _state);
                if (_activeSettings.ShowOverlay && _state != GestureRuntimeState.Idle)
                {
                    var preview = _recognizer.Recognize(_points, _activeSettings.Options);
                    var previewPattern = NormalizePreviewPattern(preview.Pattern);
                    var hudInfo = _hudInfoProvider.GetInfo(_activeSettings.Preset, previewPattern);
                    _ = _gestureOverlayService.UpdateGestureAsync([.. _points], hudInfo, CancellationToken.None);
                }

                return null;

            case MouseHookEventType.RightButtonUp:
                LogDebug(_activeSettings, "RightButtonUp: state={State}, pointCount={PointCount}", _state, _points.Count);
                if (_synthesizeRightClickOnNextRightButtonUp)
                {
                    args.Suppress = true;
                    _synthesizeRightClickOnNextRightButtonUp = false;
                    _ = Task.Run(() => SynthesizeRightClickAsync(mouseEvent.X, mouseEvent.Y));
                    return null;
                }

                if (_state == GestureRuntimeState.Idle)
                {
                    return null;
                }

                args.Suppress = true;
                LogDebug(_activeSettings, "Suppressed original right button");

                if (_activeSettings is null)
                {
                    ResetState("StateReset");
                    return null;
                }

                var upPoint = ToPoint(mouseEvent);
                if (Distance(_points[^1], upPoint) >= 1)
                {
                    _points.Add(upPoint);
                    TrimTrackedPoints();
                }

                if (_state != GestureRuntimeState.GestureActive || IsExpired(mouseEvent.Time, _activeSettings))
                {
                    var clickX = mouseEvent.X;
                    var clickY = mouseEvent.Y;
                    ResetState(IsExpired(mouseEvent.Time, _activeSettings) ? "TimeoutReset" : "StateReset");
                    _ = _gestureOverlayService.HideAsync(CancellationToken.None);
                    _ = Task.Run(() => SynthesizeRightClickAsync(clickX, clickY));
                    return null;
                }

                var pendingGesture = new PendingGesture([.. _points], _activeSettings.Options, _activeSettings.Preset, _activeSettings.ShowOverlay);
                ResetState("StateReset");
                return pendingGesture;

            default:
                return null;
        }
    }

    private async Task RecognizeAndExecuteGestureAsync(PendingGesture pendingGesture)
    {
        try
        {
            var result = _recognizer.Recognize(pendingGesture.Points, pendingGesture.Options);
            if (result.IsValid && result.Pattern is not null)
            {
                lock (_syncRoot)
                {
                    _lastPattern = result.Pattern;
                }

                _logger.LogDebug("Recognized Pattern: {Pattern}", result.Pattern);
                if (pendingGesture.ShowOverlay)
                {
                    var hudInfo = _hudInfoProvider.GetInfo(pendingGesture.Preset, result.Pattern);
                    await _gestureOverlayService.CompleteGestureAsync(hudInfo, CancellationToken.None);
                }

                await ExecuteGestureAsync(result.Pattern);
                if (pendingGesture.ShowOverlay)
                {
                    await _gestureOverlayService.HideAsync(CancellationToken.None);
                }

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

            _logger.LogError(ex, "Gesture recognition failed.");
        }
    }

    private async Task ExecuteGestureAsync(string pattern)
    {
        try
        {
            var settings = _settingsProvider.GetCurrent();
            var action = _presetProvider.GetAction(settings.Preset, pattern);
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

            await _actionExecutor.ExecuteAsync(action, CancellationToken.None);
            LogDebug(settings, "Action Executed: {Action}", action);
        }
        catch (Exception ex)
        {
            lock (_syncRoot)
            {
                SetLastError(ex.Message);
                ResetState("ExceptionReset");
            }

            _logger.LogError(ex, "Gesture action execution failed.");
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
    }

    private void TrimTrackedPoints()
    {
        if (_points.Count <= MaxTrackedPoints)
        {
            return;
        }

        var overflow = _points.Count - MaxTrackedPoints;
        _points.RemoveRange(0, overflow);
        if (_startPoint is not null && !_points.Contains(_startPoint))
        {
            _points.Insert(0, _startPoint);
            if (_points.Count > MaxTrackedPoints)
            {
                _points.RemoveAt(1);
            }
        }
    }

    private static string? NormalizePreviewPattern(string? pattern)
    {
        return pattern is { Length: > MaxPreviewPatternLength } ? null : pattern;
    }

    private Task SynthesizeRightClickAsync(int x, int y)
    {
        try
        {
            lock (_syncRoot)
            {
                _ignoreInjectedUntil = DateTimeOffset.UtcNow.AddMilliseconds(500);
            }

            _rightClickSynthesizer.SynthesizeRightClick(x, y);
            LogDebug(_settingsProvider.GetCurrent(), "Synthesized normal right click");
        }
        catch (Exception ex)
        {
            lock (_syncRoot)
            {
                SetLastError(ex.Message);
            }

            _logger.LogError(ex, "Failed to synthesize right click.");
        }

        return Task.CompletedTask;
    }

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

    private static double Distance(GesturePoint first, GesturePoint second)
    {
        var dx = second.X - first.X;
        var dy = second.Y - first.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    private sealed record PendingGesture(
        IReadOnlyList<GesturePoint> Points,
        GestureOptions Options,
        GesturePreset Preset,
        bool ShowOverlay);
}
