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
    private DateTimeOffset _cooldownUntil = DateTimeOffset.MinValue;
    private EdgeTriggerSettings _currentSettings;
    private bool _started;

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

    public Task StartAsync(CancellationToken cancellationToken)
    {
        var settings = LoadSettings();
        IsEnabled = settings.Enabled;
        if (!IsEnabled)
        {
            return Task.CompletedTask;
        }

        lock (_syncRoot)
        {
            if (_loopTask is { IsCompleted: false })
            {
                return Task.CompletedTask;
            }

            _loopCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _mouseHook.MouseEventReceived += OnMouseEventReceived;
            _mouseHook.Start();
            _started = true;
            _loopTask = Task.Run(() => RunAsync(_loopCancellation.Token), CancellationToken.None);
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
            return;
        }

        if (position.Time < _cooldownUntil)
        {
            return;
        }

        if (_activeCorner != corner)
        {
            _activeCorner = corner;
            _enteredCornerAt = position.Time;
            return;
        }

        if (_enteredCornerAt is null ||
            position.Time - _enteredCornerAt.Value < TimeSpan.FromMilliseconds(settings.DwellMs))
        {
            return;
        }

        var action = settings.GetAction(corner.Value);
        ResetTracking();
        _cooldownUntil = position.Time.AddMilliseconds(settings.CooldownMs);
        if (action == BuiltInGestureAction.None)
        {
            return;
        }

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
            lock (_syncRoot)
            {
                action = ResolveMouseEdgeAction(args.Event, _currentSettings);
                if (action != BuiltInGestureAction.None)
                {
                    _cooldownUntil = args.Event.Time.AddMilliseconds(_currentSettings.CooldownMs);
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

    private BuiltInGestureAction ResolveMouseEdgeAction(MouseHookEvent mouseEvent, EdgeTriggerSettings settings)
    {
        if (!settings.Enabled || mouseEvent.Time < _cooldownUntil)
        {
            return BuiltInGestureAction.None;
        }

        var bounds = _cursorPositionProvider.GetVirtualScreenBounds();
        if (mouseEvent.Type == MouseHookEventType.Wheel)
        {
            if (!settings.TopRightWheelEnabled || !IsTopRightCorner(mouseEvent.X, mouseEvent.Y, bounds, settings.HotZoneSize))
            {
                return BuiltInGestureAction.None;
            }

            return settings.TopRightWheelAction;
        }

        if (!IsLeftEdge(mouseEvent.X, bounds, settings.HotZoneSize))
        {
            return BuiltInGestureAction.None;
        }

        return settings.GetLeftEdgeButtonAction(mouseEvent.Type);
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
            _settingsService.Get(SettingKeys.EdgeTriggerEnabled, false),
            Math.Clamp(_settingsService.Get(SettingKeys.EdgeTriggerHotZoneSize, 8), 2, 64),
            Math.Clamp(_settingsService.Get(SettingKeys.EdgeTriggerDwellMs, 350), 100, 2000),
            Math.Clamp(_settingsService.Get(SettingKeys.EdgeTriggerCooldownMs, 1200), 250, 5000),
            _settingsService.Get(SettingKeys.EdgeTriggerTopLeftAction, BuiltInGestureAction.StartMenu),
            _settingsService.Get(SettingKeys.EdgeTriggerTopRightAction, BuiltInGestureAction.TaskSwitcher),
            _settingsService.Get(SettingKeys.EdgeTriggerBottomRightAction, BuiltInGestureAction.ShowDesktop),
            _settingsService.Get(SettingKeys.EdgeTriggerBottomLeftAction, BuiltInGestureAction.SwitchApp),
            _settingsService.Get(SettingKeys.EdgeTriggerLeftEdgeLeftButtonEnabled, false),
            _settingsService.Get(SettingKeys.EdgeTriggerLeftEdgeLeftButtonAction, BuiltInGestureAction.StartMenu),
            _settingsService.Get(SettingKeys.EdgeTriggerLeftEdgeMiddleButtonEnabled, false),
            _settingsService.Get(SettingKeys.EdgeTriggerLeftEdgeMiddleButtonAction, BuiltInGestureAction.ShowDesktop),
            _settingsService.Get(SettingKeys.EdgeTriggerLeftEdgeXButton1Enabled, false),
            _settingsService.Get(SettingKeys.EdgeTriggerLeftEdgeXButton1Action, BuiltInGestureAction.SwitchApp),
            _settingsService.Get(SettingKeys.EdgeTriggerLeftEdgeXButton2Enabled, false),
            _settingsService.Get(SettingKeys.EdgeTriggerLeftEdgeXButton2Action, BuiltInGestureAction.TaskSwitcher),
            _settingsService.Get(SettingKeys.EdgeTriggerTopRightWheelEnabled, false),
            _settingsService.Get(SettingKeys.EdgeTriggerTopRightWheelAction, BuiltInGestureAction.TaskSwitcher));
    }

    private void ResetTracking()
    {
        _activeCorner = null;
        _enteredCornerAt = null;
    }
}
