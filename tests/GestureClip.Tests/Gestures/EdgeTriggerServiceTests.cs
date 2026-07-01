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

    private static EdgeTriggerService CreateService(
        FakeCursorPositionProvider? cursor = null,
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

        return new EdgeTriggerService(
            cursor ?? new FakeCursorPositionProvider(),
            executor ?? new FakeGestureActionExecutor(),
            settings,
            NullLogger<EdgeTriggerService>.Instance);
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
