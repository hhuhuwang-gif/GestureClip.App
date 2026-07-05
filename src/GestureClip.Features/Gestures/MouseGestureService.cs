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
    private readonly IWorkstationDashboardService _workstationDashboardService;
    private readonly IWorkerLevelService _workerLevelService;
    private readonly IWorkerLevelUpService _workerLevelUpService;
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
    private GestureTriggerButton _pendingSyntheticClickButton = GestureTriggerButton.Right;
    private GesturePoint? _pendingSyntheticClickPoint;
    private GestureTriggerButton _activeTriggerButton = GestureTriggerButton.Right;
    private bool _rightLeftRockerDown;
    private bool _leftClickConfirmDown;
    private GestureTriggerButton? _suppressNextButtonUp;
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
        IWorkstationDashboardService workstationDashboardService,
        IWorkerLevelService workerLevelService,
        IWorkerLevelUpService workerLevelUpService,
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
        _workstationDashboardService = workstationDashboardService;
        _workerLevelService = workerLevelService;
        _workerLevelUpService = workerLevelUpService;
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
            _logger.LogDebug("GestureRecovery InjectedMouseClickIgnored");
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

        switch (mouseEvent.Type)
        {
            case MouseHookEventType.LeftButtonDown:
            case MouseHookEventType.RightButtonDown:
            case MouseHookEventType.MiddleButtonDown:
            case MouseHookEventType.XButton1Down:
            case MouseHookEventType.XButton2Down:
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
                args.Suppress = true;
                LogDebug(_activeSettings, "{Button}Down: x={X}, y={Y}", triggerButton, mouseEvent.X, mouseEvent.Y);
                LogDebug(_activeSettings, "Suppressed original {Button} button", triggerButton);
                return null;

            case MouseHookEventType.Move:
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
                    if (_activeSettings.ShowOverlay)
                    {
                        var startHudInfo = _hudInfoProvider.GetInfo(_activeSettings.Preset, null);
                        _ = _gestureOverlayService.ShowGestureStartAsync(_startPoint, startHudInfo, CancellationToken.None);
                    }
                }

                LogMoveDebug(_activeSettings, mouseEvent.X, mouseEvent.Y, distanceFromStart, _state);
                if (_activeSettings.ShowOverlay && _state == GestureRuntimeState.GestureActive)
                {
                    var preview = _recognizer.Recognize(_points, _activeSettings.Options);
                    var previewPattern = NormalizePreviewPattern(preview.Pattern);
                    var hudInfo = _hudInfoProvider.GetInfo(_activeSettings.Preset, previewPattern);
                    _ = _gestureOverlayService.UpdateGestureAsync([.. _points], hudInfo, CancellationToken.None);
                }

                return null;

            case MouseHookEventType.LeftButtonUp:
            case MouseHookEventType.RightButtonUp:
            case MouseHookEventType.MiddleButtonUp:
            case MouseHookEventType.XButton1Up:
            case MouseHookEventType.XButton2Up:
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
                    _ = Task.Run(() => SynthesizeClickAsync(buttonToSynthesize, clickPoint.X, clickPoint.Y));
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
                    var expired = IsExpired(mouseEvent.Time, _activeSettings);
                    var shouldHideOverlay = _state == GestureRuntimeState.GestureActive || expired;
                    var clickPoint = _startPoint ?? upPoint;
                    var clickButton = _activeTriggerButton;
                    ResetState(expired ? "TimeoutReset" : "StateReset");
                    if (shouldHideOverlay)
                    {
                        _ = _gestureOverlayService.HideAsync(CancellationToken.None);
                    }

                    _ = Task.Run(() => SynthesizeClickAsync(clickButton, clickPoint.X, clickPoint.Y));
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
                lock (_syncRoot)
                {
                    _lastPattern = pattern;
                }

                _logger.LogDebug("Recognized Pattern: {Pattern}", pattern);
                if (pendingGesture.ShowOverlay)
                {
                    var hudInfo = _hudInfoProvider.GetInfo(pendingGesture.Preset, pattern);
                    await _gestureOverlayService.CompleteGestureAsync(hudInfo, CancellationToken.None);
                }

                await ExecuteGestureAsync(pattern);
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

            _logger.LogError(ex, "Gesture action execution failed.");
        }
    }

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
        _leftClickConfirmDown = false;
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
        if (_points.Count > 0 && Distance(_points[^1], upPoint) >= 1)
        {
            _points.Add(upPoint);
            TrimTrackedPoints();
        }

        pendingGesture = new PendingGesture([.. _points], _activeSettings.Options, _activeSettings.Preset, _activeSettings.ShowOverlay);
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
            "R+L");
        ResetState("StateReset");
        return true;
    }

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

    private Task SynthesizeClickAsync(GestureTriggerButton button, int x, int y)
    {
        try
        {
            lock (_syncRoot)
            {
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
        bool ShowOverlay,
        string? PatternOverride = null);
}


