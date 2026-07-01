using GestureClip.Core.Abstractions;
using GestureClip.Core.Gestures;
using GestureClip.Core.Settings;
using Microsoft.Extensions.Logging;

namespace GestureClip.Features.Gestures;

public sealed class EdgeTriggerService : IEdgeTriggerService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(80);

    private readonly ICursorPositionProvider _cursorPositionProvider;
    private readonly ILowLevelMouseHook _mouseHook;
    private readonly IMouseGestureActionExecutor _actionExecutor;
    private readonly ISettingsService _settingsService;
    private readonly ILogger<EdgeTriggerService> _logger;
    private readonly object _syncRoot = new();
    private CancellationTokenSource? _loopCancellation;
    private Task? _loopTask;
    private ScreenCornerTarget? _activeCorner;
    private DateTimeOffset? _enteredCornerAt;
    private ScreenEdgeTarget? _activeSlideEdge;
    private CursorPosition? _slideStartPoint;
    private DateTimeOffset _cooldownUntil = DateTimeOffset.MinValue;
    private EdgeTriggerSettings _currentSettings;
    private bool _started;
    private EdgeTriggerDiagnosticsSnapshot _diagnostics = new(false, "-", "-", BuiltInGestureAction.None, "未触发", null, null);

    public EdgeTriggerService(
        ICursorPositionProvider cursorPositionProvider,
        ILowLevelMouseHook mouseHook,
        IMouseGestureActionExecutor actionExecutor,
        ISettingsService settingsService,
        ILogger<EdgeTriggerService> logger)
    {
        _cursorPositionProvider = cursorPositionProvider;
        _mouseHook = mouseHook;
        _actionExecutor = actionExecutor;
        _settingsService = settingsService;
        _logger = logger;
        _currentSettings = LoadSettings();
        IsEnabled = _currentSettings.Enabled;
    }

    public bool IsEnabled { get; private set; }

    public EdgeTriggerDiagnosticsSnapshot Diagnostics
    {
        get
        {
            lock (_syncRoot)
            {
                return _diagnostics;
            }
        }
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        var settings = LoadSettings();
        IsEnabled = settings.Enabled;
        if (!IsEnabled)
        {
            SetDiagnostics("服务状态", "-", BuiltInGestureAction.None, "边缘触发未启用", null);
            return Task.CompletedTask;
        }

        lock (_syncRoot)
        {
            if (_loopTask is { IsCompleted: false })
            {
                return Task.CompletedTask;
            }

            var loopCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var loopToken = loopCancellation.Token;
            _loopCancellation = loopCancellation;
            _mouseHook.MouseEventReceived += OnMouseEventReceived;
            _mouseHook.Start();
            _started = true;
            _loopTask = Task.Run(() => RunAsync(loopToken), CancellationToken.None);
        }

        _logger.LogInformation("Edge trigger service started.");
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        CancellationTokenSource? cts;
        Task? task;
        lock (_syncRoot)
        {
            cts = _loopCancellation;
            task = _loopTask;
            _loopCancellation = null;
            _loopTask = null;
            if (!_started)
            {
                cts = null;
                task = null;
            }

            _started = false;
            ResetTracking();
            SetDiagnostics("服务状态", "-", BuiltInGestureAction.None, "边缘触发已停止", null);
        }

        if (cts is not null || task is not null)
        {
            _mouseHook.MouseEventReceived -= OnMouseEventReceived;
            _mouseHook.Stop();
        }

        if (cts is not null)
        {
            await cts.CancelAsync();
            cts.Dispose();
        }

        if (task is not null)
        {
            try
            {
                await task.WaitAsync(TimeSpan.FromSeconds(1), cancellationToken);
            }
            catch (OperationCanceledException)
            {
            }
            catch (TimeoutException)
            {
                _logger.LogWarning("Edge trigger service did not stop within timeout.");
            }
        }

        IsEnabled = false;
    }

    public async Task PollOnceAsync(CancellationToken cancellationToken)
    {
        var settings = LoadSettings();
        lock (_syncRoot)
        {
            _currentSettings = settings;
        }

        IsEnabled = settings.Enabled;
        if (!settings.Enabled)
        {
            ResetTracking();
            SetDiagnostics("轮询", "-", BuiltInGestureAction.None, "边缘触发未启用", null);
            return;
        }

        var position = _cursorPositionProvider.GetCurrentPosition();
        var bounds = _cursorPositionProvider.GetVirtualScreenBounds();
        await EvaluateAsync(position, bounds, settings, cancellationToken);
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(PollInterval);
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                await PollOnceAsync(cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Edge trigger loop failed.");
        }
    }

    private async Task EvaluateAsync(
        CursorPosition position,
        ScreenBounds bounds,
        EdgeTriggerSettings settings,
        CancellationToken cancellationToken)
    {
        var corner = HitTestCorner(position, bounds, settings.HotZoneSize);
        if (corner is null)
        {
            ResetTracking();
            SetDiagnostics("四角热区", $"{position.X}, {position.Y}", BuiltInGestureAction.None, "未进入角落热区", position.Time);
            return;
        }

        if (position.Time < _cooldownUntil)
        {
            SetDiagnostics($"四角热区 {corner}", $"{position.X}, {position.Y}", BuiltInGestureAction.None, "冷却中", position.Time);
            return;
        }

        if (_activeCorner != corner)
        {
            _activeCorner = corner;
            _enteredCornerAt = position.Time;
            SetDiagnostics($"四角热区 {corner}", $"{position.X}, {position.Y}", BuiltInGestureAction.None, "等待停留", position.Time);
            return;
        }

        if (_enteredCornerAt is null ||
            position.Time - _enteredCornerAt.Value < TimeSpan.FromMilliseconds(settings.DwellMs))
        {
            SetDiagnostics($"四角热区 {corner}", $"{position.X}, {position.Y}", BuiltInGestureAction.None, "停留时间不足", position.Time);
            return;
        }

        var action = settings.GetAction(corner.Value);
        ResetTracking();
        _cooldownUntil = position.Time.AddMilliseconds(settings.CooldownMs);
        if (action == BuiltInGestureAction.None)
        {
            SetDiagnostics($"四角热区 {corner}", $"{position.X}, {position.Y}", BuiltInGestureAction.None, "动作未绑定", position.Time);
            return;
        }

        SetDiagnostics($"四角热区 {corner}", $"{position.X}, {position.Y}", action, "已触发", position.Time);
        _logger.LogInformation("Edge trigger action requested. Corner={Corner}, Action={Action}", corner, action);
        await _actionExecutor.ExecuteAsync(action, cancellationToken);
    }

    private void OnMouseEventReceived(object? sender, MouseHookEventArgs args)
    {
        try
        {
            if (args.Event.IsInjected)
            {
                return;
            }

            BuiltInGestureAction action;
            string source;
            lock (_syncRoot)
            {
                action = ResolveMouseEdgeAction(args.Event, _currentSettings, out source);
                if (action != BuiltInGestureAction.None)
                {
                    _cooldownUntil = args.Event.Time.AddMilliseconds(_currentSettings.CooldownMs);
                    SetDiagnostics(source, $"{args.Event.X}, {args.Event.Y}", action, "已触发", args.Event.Time);
                }
            }

            if (action == BuiltInGestureAction.None)
            {
                return;
            }

            _ = Task.Run(() => ExecuteEdgeActionAsync(action, CancellationToken.None));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Edge trigger mouse event handling failed.");
        }
    }

    private BuiltInGestureAction ResolveMouseEdgeAction(MouseHookEvent mouseEvent, EdgeTriggerSettings settings, out string source)
    {
        source = "边缘触发";
        if (!settings.Enabled || mouseEvent.Time < _cooldownUntil)
        {
            SetDiagnostics(source, $"{mouseEvent.X}, {mouseEvent.Y}", BuiltInGestureAction.None, settings.Enabled ? "冷却中" : "边缘触发未启用", mouseEvent.Time);
            return BuiltInGestureAction.None;
        }

        var bounds = _cursorPositionProvider.GetVirtualScreenBounds();
        var slideAction = ResolveSlideAction(mouseEvent, bounds, settings, out source);
        if (slideAction != BuiltInGestureAction.None)
        {
            return slideAction;
        }

        if (mouseEvent.Type == MouseHookEventType.Wheel)
        {
            source = "右上角 + 滚轮";
            if (!settings.TopRightWheelEnabled || !IsTopRightCorner(mouseEvent.X, mouseEvent.Y, bounds, settings.HotZoneSize))
            {
                SetDiagnostics(source, $"{mouseEvent.X}, {mouseEvent.Y}", BuiltInGestureAction.None, settings.TopRightWheelEnabled ? "未进入右上角热区" : "触发方式未启用", mouseEvent.Time);
                return BuiltInGestureAction.None;
            }

            return settings.TopRightWheelAction;
        }

        source = mouseEvent.Type switch
        {
            MouseHookEventType.LeftButtonDown => "左边缘 + 鼠标左键",
            MouseHookEventType.MiddleButtonDown => "左边缘 + 鼠标中键",
            MouseHookEventType.XButton1Down => "左边缘 + 鼠标侧键 1",
            MouseHookEventType.XButton2Down => "左边缘 + 鼠标侧键 2",
            _ => "左边缘 + 鼠标按钮"
        };
        if (!IsLeftEdge(mouseEvent.X, bounds, settings.HotZoneSize))
        {
            if (IsButtonDown(mouseEvent.Type))
            {
                SetDiagnostics(source, $"{mouseEvent.X}, {mouseEvent.Y}", BuiltInGestureAction.None, "未进入左边缘热区", mouseEvent.Time);
            }

            return BuiltInGestureAction.None;
        }

        return settings.GetLeftEdgeButtonAction(mouseEvent.Type);
    }

    private BuiltInGestureAction ResolveSlideAction(MouseHookEvent mouseEvent, ScreenBounds bounds, EdgeTriggerSettings settings, out string source)
    {
        source = "边缘滑动";
        if (mouseEvent.Type != MouseHookEventType.Move)
        {
            return BuiltInGestureAction.None;
        }

        var position = new CursorPosition(mouseEvent.X, mouseEvent.Y, mouseEvent.Time);
        var edge = HitTestEdge(position, bounds, settings.HotZoneSize);
        if (_activeSlideEdge is null)
        {
            if (edge is not null)
            {
                _activeSlideEdge = edge;
                _slideStartPoint = position;
                SetDiagnostics($"边缘滑动 {edge}", $"{position.X}, {position.Y}", BuiltInGestureAction.None, "等待向屏幕内滑动", position.Time);
            }

            return BuiltInGestureAction.None;
        }

        if (_slideStartPoint is null)
        {
            ResetSlideTracking();
            return BuiltInGestureAction.None;
        }

        source = $"边缘滑动 {_activeSlideEdge}";
        var distance = InwardDistance(_activeSlideEdge.Value, _slideStartPoint, position);
        if (distance < settings.SlideThreshold)
        {
            SetDiagnostics(source, $"{position.X}, {position.Y}", BuiltInGestureAction.None, $"滑动距离不足 {distance}px", position.Time);
            return BuiltInGestureAction.None;
        }

        var action = settings.GetSlideAction(_activeSlideEdge.Value);
        ResetSlideTracking();
        return action;
    }

    private async Task ExecuteEdgeActionAsync(BuiltInGestureAction action, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Edge mouse trigger action requested. Action={Action}", action);
            await _actionExecutor.ExecuteAsync(action, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Edge mouse trigger action failed.");
        }
    }

    private static ScreenCornerTarget? HitTestCorner(CursorPosition position, ScreenBounds bounds, int hotZoneSize)
    {
        var nearLeft = position.X <= bounds.Left + hotZoneSize;
        var nearRight = position.X >= bounds.Right - hotZoneSize;
        var nearTop = position.Y <= bounds.Top + hotZoneSize;
        var nearBottom = position.Y >= bounds.Bottom - hotZoneSize;

        if (nearLeft && nearTop)
        {
            return ScreenCornerTarget.TopLeft;
        }

        if (nearRight && nearTop)
        {
            return ScreenCornerTarget.TopRight;
        }

        if (nearRight && nearBottom)
        {
            return ScreenCornerTarget.BottomRight;
        }

        if (nearLeft && nearBottom)
        {
            return ScreenCornerTarget.BottomLeft;
        }

        return null;
    }

    private static bool IsLeftEdge(int x, ScreenBounds bounds, int hotZoneSize)
    {
        return x <= bounds.Left + hotZoneSize;
    }

    private static bool IsTopRightCorner(int x, int y, ScreenBounds bounds, int hotZoneSize)
    {
        return x >= bounds.Right - hotZoneSize && y <= bounds.Top + hotZoneSize;
    }

    private EdgeTriggerSettings LoadSettings()
    {
        return new EdgeTriggerSettings(
            _settingsService.Get(SettingKeys.EdgeTriggerEnabled, true),
            Math.Clamp(_settingsService.Get(SettingKeys.EdgeTriggerHotZoneSize, 8), 2, 64),
            Math.Clamp(_settingsService.Get(SettingKeys.EdgeTriggerDwellMs, 350), 100, 2000),
            Math.Clamp(_settingsService.Get(SettingKeys.EdgeTriggerCooldownMs, 1200), 250, 5000),
            Math.Clamp(_settingsService.Get(SettingKeys.EdgeTriggerSlideThreshold, 80), 24, 400),
            _settingsService.Get(SettingKeys.EdgeTriggerTopLeftAction, BuiltInGestureAction.StartMenu),
            _settingsService.Get(SettingKeys.EdgeTriggerTopRightAction, BuiltInGestureAction.TaskSwitcher),
            _settingsService.Get(SettingKeys.EdgeTriggerBottomRightAction, BuiltInGestureAction.ShowDesktop),
            _settingsService.Get(SettingKeys.EdgeTriggerBottomLeftAction, BuiltInGestureAction.SwitchApp),
            _settingsService.Get(SettingKeys.EdgeTriggerLeftEdgeLeftButtonEnabled, true),
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
            _settingsService.Get(SettingKeys.EdgeTriggerSlideBottomAction, BuiltInGestureAction.ShowDesktop));
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

    private void SetDiagnostics(string source, string position, BuiltInGestureAction action, string reason, DateTimeOffset? eventAt)
    {
        _diagnostics = new EdgeTriggerDiagnosticsSnapshot(IsEnabled, source, position, action, reason, eventAt, _cooldownUntil > DateTimeOffset.UtcNow ? _cooldownUntil : null);
    }

    private static bool IsButtonDown(MouseHookEventType type)
    {
        return type is MouseHookEventType.LeftButtonDown or MouseHookEventType.MiddleButtonDown or MouseHookEventType.XButton1Down or MouseHookEventType.XButton2Down;
    }

    private static ScreenEdgeTarget? HitTestEdge(CursorPosition position, ScreenBounds bounds, int hotZoneSize)
    {
        if (position.X <= bounds.Left + hotZoneSize)
        {
            return ScreenEdgeTarget.Left;
        }

        if (position.X >= bounds.Right - hotZoneSize)
        {
            return ScreenEdgeTarget.Right;
        }

        if (position.Y <= bounds.Top + hotZoneSize)
        {
            return ScreenEdgeTarget.Top;
        }

        if (position.Y >= bounds.Bottom - hotZoneSize)
        {
            return ScreenEdgeTarget.Bottom;
        }

        return null;
    }

    private static int InwardDistance(ScreenEdgeTarget edge, CursorPosition start, CursorPosition current)
    {
        return edge switch
        {
            ScreenEdgeTarget.Left => current.X - start.X,
            ScreenEdgeTarget.Right => start.X - current.X,
            ScreenEdgeTarget.Top => current.Y - start.Y,
            ScreenEdgeTarget.Bottom => start.Y - current.Y,
            _ => 0
        };
    }
}
