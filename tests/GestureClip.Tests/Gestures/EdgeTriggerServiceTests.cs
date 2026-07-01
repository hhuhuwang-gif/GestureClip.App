using GestureClip.Core.Abstractions;
using GestureClip.Core.Gestures;
using GestureClip.Core.Settings;
using GestureClip.Features.Gestures;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GestureClip.Tests.Gestures;

public sealed class EdgeTriggerServiceTests
{
    [Fact]
    public async Task PollOnceAsync_does_not_execute_when_disabled()
    {
        var executor = new FakeGestureActionExecutor();
        var settings = new FakeSettingsService();
        settings.Values[SettingKeys.EdgeTriggerEnabled] = false;
        var service = CreateService(executor: executor, settings: settings);

        await service.PollOnceAsync(CancellationToken.None);

        Assert.Empty(executor.Actions);
    }

    [Fact]
    public async Task PollOnceAsync_requires_dwell_before_executing()
    {
        var now = DateTimeOffset.UtcNow;
        var cursor = new FakeCursorPositionProvider
        {
            Position = new CursorPosition(2, 2, now)
        };
        var executor = new FakeGestureActionExecutor();
        var service = CreateService(cursor: cursor, executor: executor);

        await service.PollOnceAsync(CancellationToken.None);
        cursor.Position = cursor.Position with { Time = now.AddMilliseconds(200) };
        await service.PollOnceAsync(CancellationToken.None);

        Assert.Empty(executor.Actions);

        cursor.Position = cursor.Position with { Time = now.AddMilliseconds(360) };
        await service.PollOnceAsync(CancellationToken.None);

        Assert.Equal([BuiltInGestureAction.StartMenu], executor.Actions);
    }

    [Fact]
    public async Task PollOnceAsync_uses_bottom_right_action()
    {
        var now = DateTimeOffset.UtcNow;
        var cursor = new FakeCursorPositionProvider
        {
            Position = new CursorPosition(1918, 1078, now)
        };
        var executor = new FakeGestureActionExecutor();
        var service = CreateService(cursor: cursor, executor: executor);

        await service.PollOnceAsync(CancellationToken.None);
        cursor.Position = cursor.Position with { Time = now.AddMilliseconds(400) };
        await service.PollOnceAsync(CancellationToken.None);

        Assert.Equal([BuiltInGestureAction.ShowDesktop], executor.Actions);
    }

    [Fact]
    public async Task PollOnceAsync_respects_cooldown()
    {
        var now = DateTimeOffset.UtcNow;
        var cursor = new FakeCursorPositionProvider
        {
            Position = new CursorPosition(2, 2, now)
        };
        var executor = new FakeGestureActionExecutor();
        var service = CreateService(cursor: cursor, executor: executor);

        await service.PollOnceAsync(CancellationToken.None);
        cursor.Position = cursor.Position with { Time = now.AddMilliseconds(400) };
        await service.PollOnceAsync(CancellationToken.None);

        cursor.Position = cursor.Position with { Time = now.AddMilliseconds(500) };
        await service.PollOnceAsync(CancellationToken.None);
        cursor.Position = cursor.Position with { Time = now.AddMilliseconds(900) };
        await service.PollOnceAsync(CancellationToken.None);

        Assert.Single(executor.Actions);
    }

    [Fact]
    public async Task Left_edge_middle_button_executes_configured_action()
    {
        var hook = new FakeLowLevelMouseHook();
        var executor = new FakeGestureActionExecutor();
        var settings = new FakeSettingsService();
        settings.Values[SettingKeys.EdgeTriggerLeftEdgeMiddleButtonEnabled] = true;
        var service = CreateService(hook: hook, executor: executor, settings: settings);

        await service.StartAsync(CancellationToken.None);
        hook.Emit(new MouseHookEvent(MouseHookEventType.MiddleButtonDown, 2, 500, DateTimeOffset.UtcNow));
        await WaitForAsync(() => executor.Actions.Count == 1);
        await service.StopAsync(CancellationToken.None);

        Assert.Equal([BuiltInGestureAction.ShowDesktop], executor.Actions);
    }

    [Fact]
    public async Task Right_top_wheel_executes_configured_action()
    {
        var hook = new FakeLowLevelMouseHook();
        var executor = new FakeGestureActionExecutor();
        var settings = new FakeSettingsService();
        settings.Values[SettingKeys.EdgeTriggerTopRightWheelEnabled] = true;
        var service = CreateService(hook: hook, executor: executor, settings: settings);

        await service.StartAsync(CancellationToken.None);
        hook.Emit(new MouseHookEvent(MouseHookEventType.Wheel, 1918, 2, DateTimeOffset.UtcNow, WheelDelta: 120));
        await WaitForAsync(() => executor.Actions.Count == 1);
        await service.StopAsync(CancellationToken.None);

        Assert.Equal([BuiltInGestureAction.TaskSwitcher], executor.Actions);
    }

    [Fact]
    public async Task Mouse_edge_trigger_does_not_suppress_original_event()
    {
        var hook = new FakeLowLevelMouseHook();
        var settings = new FakeSettingsService();
        settings.Values[SettingKeys.EdgeTriggerLeftEdgeXButton1Enabled] = true;
        var service = CreateService(hook: hook, settings: settings);

        await service.StartAsync(CancellationToken.None);
        var args = hook.Emit(new MouseHookEvent(MouseHookEventType.XButton1Down, 2, 500, DateTimeOffset.UtcNow));
        await service.StopAsync(CancellationToken.None);

        Assert.False(args.Suppress);
    }

    [Fact]
    public async Task Left_edge_slide_executes_after_threshold()
    {
        var hook = new FakeLowLevelMouseHook();
        var executor = new FakeGestureActionExecutor();
        var settings = new FakeSettingsService();
        settings.Values[SettingKeys.EdgeTriggerSlideLeftEnabled] = true;
        settings.Values[SettingKeys.EdgeTriggerSlideThreshold] = 80;
        var service = CreateService(hook: hook, executor: executor, settings: settings);

        await service.StartAsync(CancellationToken.None);
        hook.Emit(new MouseHookEvent(MouseHookEventType.Move, 2, 500, DateTimeOffset.UtcNow));
        hook.Emit(new MouseHookEvent(MouseHookEventType.Move, 50, 500, DateTimeOffset.UtcNow.AddMilliseconds(20)));
        Assert.Empty(executor.Actions);
        hook.Emit(new MouseHookEvent(MouseHookEventType.Move, 100, 500, DateTimeOffset.UtcNow.AddMilliseconds(40)));
        await WaitForAsync(() => executor.Actions.Count == 1);
        await service.StopAsync(CancellationToken.None);

        Assert.Equal([BuiltInGestureAction.SwitchApp], executor.Actions);
    }

    private static EdgeTriggerService CreateService(
        FakeCursorPositionProvider? cursor = null,
        FakeLowLevelMouseHook? hook = null,
        FakeGestureActionExecutor? executor = null,
        FakeSettingsService? settings = null)
    {
        settings ??= new FakeSettingsService();
        settings.Values.TryAdd(SettingKeys.EdgeTriggerEnabled, true);
        settings.Values.TryAdd(SettingKeys.EdgeTriggerHotZoneSize, 8);
        settings.Values.TryAdd(SettingKeys.EdgeTriggerDwellMs, 350);
        settings.Values.TryAdd(SettingKeys.EdgeTriggerCooldownMs, 1200);
        settings.Values.TryAdd(SettingKeys.EdgeTriggerTopLeftAction, BuiltInGestureAction.StartMenu);
        settings.Values.TryAdd(SettingKeys.EdgeTriggerTopRightAction, BuiltInGestureAction.TaskSwitcher);
        settings.Values.TryAdd(SettingKeys.EdgeTriggerBottomRightAction, BuiltInGestureAction.ShowDesktop);
        settings.Values.TryAdd(SettingKeys.EdgeTriggerBottomLeftAction, BuiltInGestureAction.SwitchApp);
        settings.Values.TryAdd(SettingKeys.EdgeTriggerLeftEdgeLeftButtonEnabled, false);
        settings.Values.TryAdd(SettingKeys.EdgeTriggerLeftEdgeLeftButtonAction, BuiltInGestureAction.StartMenu);
        settings.Values.TryAdd(SettingKeys.EdgeTriggerLeftEdgeMiddleButtonEnabled, false);
        settings.Values.TryAdd(SettingKeys.EdgeTriggerLeftEdgeMiddleButtonAction, BuiltInGestureAction.ShowDesktop);
        settings.Values.TryAdd(SettingKeys.EdgeTriggerLeftEdgeXButton1Enabled, false);
        settings.Values.TryAdd(SettingKeys.EdgeTriggerLeftEdgeXButton1Action, BuiltInGestureAction.SwitchApp);
        settings.Values.TryAdd(SettingKeys.EdgeTriggerLeftEdgeXButton2Enabled, false);
        settings.Values.TryAdd(SettingKeys.EdgeTriggerLeftEdgeXButton2Action, BuiltInGestureAction.TaskSwitcher);
        settings.Values.TryAdd(SettingKeys.EdgeTriggerTopRightWheelEnabled, false);
        settings.Values.TryAdd(SettingKeys.EdgeTriggerTopRightWheelAction, BuiltInGestureAction.TaskSwitcher);
        settings.Values.TryAdd(SettingKeys.EdgeTriggerSlideThreshold, 80);
        settings.Values.TryAdd(SettingKeys.EdgeTriggerSlideLeftEnabled, false);
        settings.Values.TryAdd(SettingKeys.EdgeTriggerSlideLeftAction, BuiltInGestureAction.SwitchApp);
        settings.Values.TryAdd(SettingKeys.EdgeTriggerSlideRightEnabled, false);
        settings.Values.TryAdd(SettingKeys.EdgeTriggerSlideRightAction, BuiltInGestureAction.TaskSwitcher);
        settings.Values.TryAdd(SettingKeys.EdgeTriggerSlideTopEnabled, false);
        settings.Values.TryAdd(SettingKeys.EdgeTriggerSlideTopAction, BuiltInGestureAction.StartMenu);
        settings.Values.TryAdd(SettingKeys.EdgeTriggerSlideBottomEnabled, false);
        settings.Values.TryAdd(SettingKeys.EdgeTriggerSlideBottomAction, BuiltInGestureAction.ShowDesktop);

        return new EdgeTriggerService(
            cursor ?? new FakeCursorPositionProvider(),
            hook ?? new FakeLowLevelMouseHook(),
            executor ?? new FakeGestureActionExecutor(),
            settings,
            NullLogger<EdgeTriggerService>.Instance);
    }

    private static async Task WaitForAsync(Func<bool> condition)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(2);
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(20);
        }

        Assert.True(condition());
    }

    private sealed class FakeCursorPositionProvider : ICursorPositionProvider
    {
        public CursorPosition Position { get; set; } = new(100, 100, DateTimeOffset.UtcNow);
        public ScreenBounds Bounds { get; set; } = new(0, 0, 1920, 1080);

        public CursorPosition GetCurrentPosition() => Position;

        public ScreenBounds GetVirtualScreenBounds() => Bounds;
    }

    private sealed class FakeGestureActionExecutor : IMouseGestureActionExecutor
    {
        public List<BuiltInGestureAction> Actions { get; } = [];

        public Task ExecuteAsync(BuiltInGestureAction action, CancellationToken cancellationToken)
        {
            Actions.Add(action);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeLowLevelMouseHook : ILowLevelMouseHook
    {
        public event EventHandler<MouseHookEventArgs>? MouseEventReceived;
        public int StartCount { get; private set; }
        public int StopCount { get; private set; }

        public void Start() => StartCount++;

        public void Stop() => StopCount++;

        public MouseHookEventArgs Emit(MouseHookEvent mouseEvent)
        {
            var args = new MouseHookEventArgs { Event = mouseEvent };
            MouseEventReceived?.Invoke(this, args);
            return args;
        }
    }

    private sealed class FakeSettingsService : ISettingsService
    {
        public Dictionary<string, object?> Values { get; } = [];

        public T Get<T>(string key, T defaultValue)
        {
            return Values.TryGetValue(key, out var value) ? (T)value! : defaultValue;
        }

        public Task SetAsync<T>(string key, T value, CancellationToken cancellationToken)
        {
            Values[key] = value;
            return Task.CompletedTask;
        }
    }
}
