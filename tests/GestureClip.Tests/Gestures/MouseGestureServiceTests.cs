using GestureClip.Core.Abstractions;
using GestureClip.Core.Gestures;
using GestureClip.Features.Gestures;
using Microsoft.Extensions.Logging.Abstractions;
using System.Collections.Concurrent;
using Xunit;

namespace GestureClip.Tests.Gestures;

public sealed class MouseGestureServiceTests
{
    [Fact]
    public async Task StartAsync_and_StopAsync_are_idempotent()
    {
        var hook = new FakeLowLevelMouseHook();
        var service = CreateService(hook);

        await service.StartAsync(CancellationToken.None);
        await service.StartAsync(CancellationToken.None);
        await service.StopAsync(CancellationToken.None);
        await service.StopAsync(CancellationToken.None);

        Assert.Equal(1, hook.StartCount);
        Assert.Equal(1, hook.StopCount);
    }

    [Fact]
    public async Task Right_click_without_threshold_suppresses_original_events_and_synthesizes_normal_right_click()
    {
        var hook = new FakeLowLevelMouseHook();
        var executor = new FakeMouseGestureActionExecutor();
        var synthesizer = new FakeRightClickSynthesizer();
        var service = CreateService(hook, executor, synthesizer: synthesizer);
        await service.StartAsync(CancellationToken.None);

        var down = hook.Raise(MouseHookEventType.RightButtonDown, 0, 0);
        var up = hook.Raise(MouseHookEventType.RightButtonUp, 3, 3);
        await WaitForAsync(() => synthesizer.Clicks.Count == 1);

        Assert.True(down.Suppress);
        Assert.True(up.Suppress);
        Assert.Equal((3, 3), synthesizer.Clicks.Single());
        Assert.Empty(executor.Actions);
    }

    [Fact]
    public async Task Gesture_active_suppresses_right_button_up_and_executes_action()
    {
        var hook = new FakeLowLevelMouseHook();
        var executor = new FakeMouseGestureActionExecutor();
        var overlay = new FakeGestureOverlayService();
        var service = CreateService(hook, executor, overlay: overlay);
        await service.StartAsync(CancellationToken.None);

        var down = hook.Raise(MouseHookEventType.RightButtonDown, 0, 0);
        var move = hook.Raise(MouseHookEventType.Move, 0, -30);
        var up = hook.Raise(MouseHookEventType.RightButtonUp, 0, -50);

        await WaitForAsync(() => executor.Actions.Count == 1);

        Assert.True(down.Suppress);
        Assert.False(move.Suppress);
        Assert.True(up.Suppress);
        Assert.Equal(BuiltInGestureAction.Copy, executor.Actions.Single());
        Assert.Contains(overlay.Events, entry => entry.StartsWith("start:", StringComparison.Ordinal));
        Assert.Contains(overlay.Events, entry => entry.StartsWith("update:", StringComparison.Ordinal));
        Assert.Contains("complete:U", overlay.Events);
        Assert.Contains("hide", overlay.Events);
        Assert.Contains(overlay.HudInfos, info => info.Pattern == "U" && info.ActionName == "复制" && info.ShortcutText == "Ctrl + C");
        Assert.Equal(GestureRuntimeState.Idle, service.Diagnostics.State);
        Assert.Equal("U", service.Diagnostics.LastPattern);
        Assert.Equal(BuiltInGestureAction.Copy, service.Diagnostics.LastAction);
    }

    [Fact]
    public async Task Right_button_down_enters_tracking_and_move_over_threshold_enters_gesture_active()
    {
        var hook = new FakeLowLevelMouseHook();
        var overlay = new FakeGestureOverlayService();
        var service = CreateService(hook, overlay: overlay);
        await service.StartAsync(CancellationToken.None);

        var down = hook.Raise(MouseHookEventType.RightButtonDown, 10, 10);
        hook.Raise(MouseHookEventType.Move, 10, -15);

        Assert.True(down.Suppress);
        Assert.Equal(GestureRuntimeState.GestureActive, service.Diagnostics.State);
        Assert.Contains(overlay.Events, entry => entry.StartsWith("start:", StringComparison.Ordinal));
        Assert.Contains(overlay.Events, entry => entry.StartsWith("update:", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Injected_right_click_events_can_trigger_gesture_when_not_synthesized_by_app()
    {
        var hook = new FakeLowLevelMouseHook();
        var executor = new FakeMouseGestureActionExecutor();
        var synthesizer = new FakeRightClickSynthesizer();
        var service = CreateService(hook, executor, synthesizer: synthesizer);
        await service.StartAsync(CancellationToken.None);

        var down = hook.Raise(MouseHookEventType.RightButtonDown, 0, 0, isInjected: true);
        hook.Raise(MouseHookEventType.Move, 0, -30, isInjected: true);
        var up = hook.Raise(MouseHookEventType.RightButtonUp, 0, -50, isInjected: true);
        await WaitForAsync(() => executor.Actions.Count == 1);

        Assert.True(down.Suppress);
        Assert.True(up.Suppress);
        Assert.Empty(synthesizer.Clicks);
        Assert.Equal(BuiltInGestureAction.Copy, executor.Actions.Single());
    }

    [Fact]
    public async Task Injected_events_during_synthesized_right_click_window_are_passed_through()
    {
        var hook = new FakeLowLevelMouseHook();
        var executor = new FakeMouseGestureActionExecutor();
        var synthesizer = new FakeRightClickSynthesizer();
        var service = CreateService(hook, executor, synthesizer: synthesizer);
        await service.StartAsync(CancellationToken.None);

        hook.Raise(MouseHookEventType.RightButtonDown, 0, 0);
        hook.Raise(MouseHookEventType.RightButtonUp, 0, 0);
        await WaitForAsync(() => synthesizer.Clicks.Count == 1);

        var down = hook.Raise(MouseHookEventType.RightButtonDown, 0, 0, isInjected: true);
        var up = hook.Raise(MouseHookEventType.RightButtonUp, 0, 0, isInjected: true);

        Assert.False(down.Suppress);
        Assert.False(up.Suppress);
        Assert.Empty(executor.Actions);
    }

    [Fact]
    public async Task StopAsync_resets_tracking_state()
    {
        var hook = new FakeLowLevelMouseHook();
        var synthesizer = new FakeRightClickSynthesizer();
        var service = CreateService(hook, synthesizer: synthesizer);
        await service.StartAsync(CancellationToken.None);

        var down = hook.Raise(MouseHookEventType.RightButtonDown, 0, 0);
        await service.StopAsync(CancellationToken.None);
        await service.StartAsync(CancellationToken.None);
        var up = hook.Raise(MouseHookEventType.RightButtonUp, 0, 0);
        await Task.Delay(50);

        Assert.True(down.Suppress);
        Assert.False(up.Suppress);
        Assert.Empty(synthesizer.Clicks);
    }

    [Fact]
    public async Task Expired_tracking_suppresses_next_right_button_up_and_synthesizes_click()
    {
        var hook = new FakeLowLevelMouseHook();
        var synthesizer = new FakeRightClickSynthesizer();
        var service = CreateService(hook, synthesizer: synthesizer, maxDurationMs: 10);
        await service.StartAsync(CancellationToken.None);

        var down = hook.Raise(MouseHookEventType.RightButtonDown, 0, 0, time: DateTimeOffset.UnixEpoch);
        hook.Raise(MouseHookEventType.Move, 1, 1, time: DateTimeOffset.UnixEpoch.AddMilliseconds(20));
        var up = hook.Raise(MouseHookEventType.RightButtonUp, 2, 2, time: DateTimeOffset.UnixEpoch.AddMilliseconds(30));
        await WaitForAsync(() => synthesizer.Clicks.Count == 1);

        Assert.True(down.Suppress);
        Assert.True(up.Suppress);
        Assert.Equal((2, 2), synthesizer.Clicks.Single());
    }

    [Fact]
    public async Task Environment_variable_disables_starting_hook()
    {
        var previous = Environment.GetEnvironmentVariable("GESTURECLIP_DISABLE_GESTURES");
        Environment.SetEnvironmentVariable("GESTURECLIP_DISABLE_GESTURES", "1");
        try
        {
            var hook = new FakeLowLevelMouseHook();
            var service = CreateService(hook);

            await service.StartAsync(CancellationToken.None);

            Assert.Equal(0, hook.StartCount);
            Assert.False(service.IsEnabled);
            Assert.Equal("被环境变量禁用", service.Diagnostics.HookStatus);
        }
        finally
        {
            Environment.SetEnvironmentVariable("GESTURECLIP_DISABLE_GESTURES", previous);
        }
    }

    [Fact]
    public async Task Disabled_service_passes_events_through()
    {
        var hook = new FakeLowLevelMouseHook();
        var executor = new FakeMouseGestureActionExecutor();
        var service = CreateService(hook, executor, enabled: false);
        await service.StartAsync(CancellationToken.None);

        var down = hook.Raise(MouseHookEventType.RightButtonDown, 0, 0);
        var move = hook.Raise(MouseHookEventType.Move, 0, -40);
        var up = hook.Raise(MouseHookEventType.RightButtonUp, 0, -60);
        await Task.Delay(50);

        Assert.False(down.Suppress);
        Assert.False(move.Suppress);
        Assert.False(up.Suppress);
        Assert.Empty(executor.Actions);
        Assert.Equal(GestureRuntimeState.Idle, service.Diagnostics.State);
    }

    [Fact]
    public async Task Blacklisted_foreground_process_passes_right_button_events_through()
    {
        var hook = new FakeLowLevelMouseHook();
        var executor = new FakeMouseGestureActionExecutor();
        var overlay = new FakeGestureOverlayService();
        var service = CreateService(hook, executor, overlay: overlay, gestureBlacklisted: true);
        await service.StartAsync(CancellationToken.None);

        var down = hook.Raise(MouseHookEventType.RightButtonDown, 0, 0);
        var move = hook.Raise(MouseHookEventType.Move, 0, -40);
        var up = hook.Raise(MouseHookEventType.RightButtonUp, 0, -60);
        await Task.Delay(50);

        Assert.False(down.Suppress);
        Assert.False(move.Suppress);
        Assert.False(up.Suppress);
        Assert.Empty(executor.Actions);
        Assert.Empty(overlay.Events);
        Assert.Equal(GestureRuntimeState.Idle, service.Diagnostics.State);
    }

    [Fact]
    public async Task Action_exception_records_error_and_leaves_state_idle()
    {
        var hook = new FakeLowLevelMouseHook();
        var service = CreateService(hook, new ThrowingMouseGestureActionExecutor());
        await service.StartAsync(CancellationToken.None);

        hook.Raise(MouseHookEventType.RightButtonDown, 0, 0);
        hook.Raise(MouseHookEventType.Move, 0, -30);
        hook.Raise(MouseHookEventType.RightButtonUp, 0, -50);

        await WaitForAsync(() => service.Diagnostics.LastError is not null);

        Assert.Equal(GestureRuntimeState.Idle, service.Diagnostics.State);
        Assert.Contains("boom", service.Diagnostics.LastError);
    }

    [Fact]
    public async Task ShowOverlay_false_does_not_call_overlay_for_gesture()
    {
        var hook = new FakeLowLevelMouseHook();
        var overlay = new FakeGestureOverlayService();
        var service = CreateService(hook, overlay: overlay, showOverlay: false);
        await service.StartAsync(CancellationToken.None);

        hook.Raise(MouseHookEventType.RightButtonDown, 0, 0);
        hook.Raise(MouseHookEventType.Move, 0, -30);
        hook.Raise(MouseHookEventType.RightButtonUp, 0, -50);
        await WaitForAsync(() => service.Diagnostics.LastPattern == "U");

        Assert.Empty(overlay.Events);
    }

    [Fact]
    public async Task Invalid_or_unbound_gesture_hides_overlay_without_executing_action()
    {
        var hook = new FakeLowLevelMouseHook();
        var executor = new FakeMouseGestureActionExecutor();
        var overlay = new FakeGestureOverlayService();
        var service = CreateService(hook, executor, overlay: overlay);
        await service.StartAsync(CancellationToken.None);

        hook.Raise(MouseHookEventType.RightButtonDown, 0, 0);
        hook.Raise(MouseHookEventType.Move, 40, 0);
        hook.Raise(MouseHookEventType.Move, 40, 40);
        hook.Raise(MouseHookEventType.RightButtonUp, 40, 40);

        await WaitForAsync(() => overlay.Events.Contains("hide"));

        Assert.Empty(executor.Actions);
        Assert.Contains(overlay.HudInfos, info => info.Pattern == "RD" && info.ActionName == "未绑定");
    }

    [Fact]
    public async Task Long_noisy_gesture_limits_overlay_points_and_hides_after_release()
    {
        var hook = new FakeLowLevelMouseHook();
        var executor = new FakeMouseGestureActionExecutor();
        var overlay = new FakeGestureOverlayService();
        var service = CreateService(hook, executor, overlay: overlay);
        await service.StartAsync(CancellationToken.None);

        hook.Raise(MouseHookEventType.RightButtonDown, 100, 100);
        for (var i = 0; i < 200; i++)
        {
            var x = 100 + (i % 4 switch
            {
                0 => 80,
                1 => 160,
                2 => 160,
                _ => 80
            });
            var y = 100 + (i * 18 % 260);
            hook.Raise(MouseHookEventType.Move, x, y);
        }

        hook.Raise(MouseHookEventType.RightButtonUp, 120, 120);
        await WaitForAsync(() => overlay.Events.Contains("hide"));

        Assert.Empty(executor.Actions);
        Assert.Equal(GestureRuntimeState.Idle, service.Diagnostics.State);
        Assert.All(overlay.PointCounts, count => Assert.InRange(count, 1, 96));
    }

    [Fact]
    public async Task Complex_gesture_stays_visible_while_dragging_and_hides_on_release()
    {
        var hook = new FakeLowLevelMouseHook();
        var executor = new FakeMouseGestureActionExecutor();
        var synthesizer = new FakeRightClickSynthesizer();
        var overlay = new FakeGestureOverlayService();
        var service = CreateService(hook, executor, synthesizer: synthesizer, overlay: overlay);
        await service.StartAsync(CancellationToken.None);

        var down = hook.Raise(MouseHookEventType.RightButtonDown, 0, 0);
        hook.Raise(MouseHookEventType.Move, 60, 0);
        hook.Raise(MouseHookEventType.Move, 60, 60);
        hook.Raise(MouseHookEventType.Move, 0, 60);
        await WaitForAsync(() => overlay.Events.Any(entry => entry.StartsWith("update:", StringComparison.Ordinal)));

        Assert.DoesNotContain("hide", overlay.Events);

        var up = hook.Raise(MouseHookEventType.RightButtonUp, 0, 60);
        await WaitForAsync(() => overlay.Events.Contains("hide"));

        Assert.True(down.Suppress);
        Assert.True(up.Suppress);
        Assert.Empty(executor.Actions);
        Assert.Empty(synthesizer.Clicks);
        Assert.Equal(GestureRuntimeState.Idle, service.Diagnostics.State);
    }

    [Fact]
    public async Task New_gesture_can_start_after_complex_gesture_was_released()
    {
        var hook = new FakeLowLevelMouseHook();
        var executor = new FakeMouseGestureActionExecutor();
        var overlay = new FakeGestureOverlayService();
        var service = CreateService(hook, executor, overlay: overlay, showOverlay: false);
        await service.StartAsync(CancellationToken.None);

        hook.Raise(MouseHookEventType.RightButtonDown, 0, 0);
        hook.Raise(MouseHookEventType.Move, 60, 0);
        hook.Raise(MouseHookEventType.Move, 60, 60);
        hook.Raise(MouseHookEventType.Move, 0, 60);
        hook.Raise(MouseHookEventType.RightButtonUp, 0, 60);

        var down = hook.Raise(MouseHookEventType.RightButtonDown, 0, 0);
        hook.Raise(MouseHookEventType.Move, 0, -40);
        var up = hook.Raise(MouseHookEventType.RightButtonUp, 0, -60);
        await WaitForAsync(() => executor.Actions.Count == 1);

        Assert.True(down.Suppress);
        Assert.True(up.Suppress);
        Assert.Equal(BuiltInGestureAction.Copy, executor.Actions.Single());
        Assert.Equal(GestureRuntimeState.Idle, service.Diagnostics.State);
    }

    [Fact]
    public async Task Consecutive_gestures_do_not_leave_state_stuck()
    {
        var hook = new FakeLowLevelMouseHook();
        var executor = new FakeMouseGestureActionExecutor();
        var service = CreateService(hook, executor, showOverlay: false);
        await service.StartAsync(CancellationToken.None);

        for (var i = 0; i < 50; i++)
        {
            var offset = i * 5;
            hook.Raise(MouseHookEventType.RightButtonDown, offset, offset);
            hook.Raise(MouseHookEventType.Move, offset, offset - 30);
            hook.Raise(MouseHookEventType.RightButtonUp, offset, offset - 50);
        }

        await WaitForAsync(() => executor.Actions.Count == 50);

        Assert.Equal(GestureRuntimeState.Idle, service.Diagnostics.State);
        Assert.Equal(50, executor.Actions.Count);
    }

    [Fact]
    public async Task Safety_timeout_resets_state_and_suppresses_next_right_button_up()
    {
        var hook = new FakeLowLevelMouseHook();
        var synthesizer = new FakeRightClickSynthesizer();
        var service = CreateService(hook, synthesizer: synthesizer, maxDurationMs: 100);
        await service.StartAsync(CancellationToken.None);

        var down = hook.Raise(MouseHookEventType.RightButtonDown, 10, 10, time: DateTimeOffset.UnixEpoch);
        hook.Raise(MouseHookEventType.Move, 12, 12, time: DateTimeOffset.UnixEpoch.AddMilliseconds(700));
        var up = hook.Raise(MouseHookEventType.RightButtonUp, 12, 12, time: DateTimeOffset.UnixEpoch.AddMilliseconds(720));

        await WaitForAsync(() => synthesizer.Clicks.Count == 1);

        Assert.True(down.Suppress);
        Assert.True(up.Suppress);
        Assert.Equal(GestureRuntimeState.Idle, service.Diagnostics.State);
        Assert.Equal((12, 12), synthesizer.Clicks.Single());
    }

    [Fact]
    public async Task Non_injected_events_are_not_ignored_during_synthesized_right_click_window()
    {
        var hook = new FakeLowLevelMouseHook();
        var executor = new FakeMouseGestureActionExecutor();
        var synthesizer = new FakeRightClickSynthesizer();
        var service = CreateService(hook, executor, synthesizer: synthesizer, showOverlay: false);
        await service.StartAsync(CancellationToken.None);

        hook.Raise(MouseHookEventType.RightButtonDown, 0, 0);
        hook.Raise(MouseHookEventType.RightButtonUp, 0, 0);
        await WaitForAsync(() => synthesizer.Clicks.Count == 1);

        var down = hook.Raise(MouseHookEventType.RightButtonDown, 0, 0, isInjected: false);
        hook.Raise(MouseHookEventType.Move, 0, -30, isInjected: false);
        var up = hook.Raise(MouseHookEventType.RightButtonUp, 0, -50, isInjected: false);
        await WaitForAsync(() => executor.Actions.Count == 1);

        Assert.True(down.Suppress);
        Assert.True(up.Suppress);
        Assert.Equal(BuiltInGestureAction.Copy, executor.Actions.Single());
    }

    private static MouseGestureService CreateService(
        FakeLowLevelMouseHook hook,
        IMouseGestureActionExecutor? executor = null,
        FakeRightClickSynthesizer? synthesizer = null,
        FakeGestureOverlayService? overlay = null,
        bool enabled = true,
        int maxDurationMs = 2000,
        bool showOverlay = true,
        bool debugEnabled = false,
        bool gestureBlacklisted = false)
    {
        return new MouseGestureService(
            hook,
            new DirectionGestureRecognizer(),
            executor ?? new FakeMouseGestureActionExecutor(),
            synthesizer ?? new FakeRightClickSynthesizer(),
            new FakeGestureSettingsProvider(enabled, maxDurationMs, showOverlay, debugEnabled),
            new GesturePresetProvider(),
            new GestureHudInfoProvider(new GesturePresetProvider()),
            overlay ?? new FakeGestureOverlayService(),
            new FakeForegroundAppService(),
            new FakeAppBlacklistService { GestureBlocked = gestureBlacklisted },
            NullLogger<MouseGestureService>.Instance);
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

    private sealed class FakeLowLevelMouseHook : ILowLevelMouseHook
    {
        public event EventHandler<MouseHookEventArgs>? MouseEventReceived;

        public int StartCount { get; private set; }
        public int StopCount { get; private set; }

        public void Start() => StartCount++;

        public void Stop() => StopCount++;

        public MouseHookEventArgs Raise(
            MouseHookEventType type,
            int x,
            int y,
            bool isInjected = false,
            DateTimeOffset? time = null)
        {
            var args = new MouseHookEventArgs
            {
                Event = new MouseHookEvent(type, x, y, time ?? DateTimeOffset.UtcNow, isInjected)
            };
            MouseEventReceived?.Invoke(this, args);
            return args;
        }
    }

    private sealed class FakeRightClickSynthesizer : IRightClickSynthesizer
    {
        public List<(int X, int Y)> Clicks { get; } = [];

        public void SynthesizeRightClick(int x, int y)
        {
            Clicks.Add((x, y));
        }
    }

    private sealed class FakeMouseGestureActionExecutor : IMouseGestureActionExecutor
    {
        public ConcurrentQueue<BuiltInGestureAction> Actions { get; } = [];

        public Task ExecuteAsync(BuiltInGestureAction action, CancellationToken cancellationToken)
        {
            Actions.Enqueue(action);
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingMouseGestureActionExecutor : IMouseGestureActionExecutor
    {
        public Task ExecuteAsync(BuiltInGestureAction action, CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("boom");
        }
    }

    private sealed class FakeGestureOverlayService : IGestureOverlayService
    {
        public List<string> Events { get; } = [];

        public List<GestureHudInfo> HudInfos { get; } = [];

        public List<int> PointCounts { get; } = [];

        public Task ShowGestureStartAsync(GesturePoint point, GestureHudInfo hudInfo, CancellationToken cancellationToken)
        {
            HudInfos.Add(hudInfo);
            PointCounts.Add(1);
            Events.Add($"start:{point.X},{point.Y}");
            return Task.CompletedTask;
        }

        public Task UpdateGestureAsync(IReadOnlyList<GesturePoint> points, GestureHudInfo hudInfo, CancellationToken cancellationToken)
        {
            HudInfos.Add(hudInfo);
            PointCounts.Add(points.Count);
            Events.Add($"update:{points.Count}:{hudInfo.Pattern}");
            return Task.CompletedTask;
        }

        public Task CompleteGestureAsync(GestureHudInfo hudInfo, CancellationToken cancellationToken)
        {
            HudInfos.Add(hudInfo);
            Events.Add($"complete:{hudInfo.Pattern}");
            return Task.CompletedTask;
        }

        public Task ShowPatternAsync(string pattern, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task HideAsync(CancellationToken cancellationToken)
        {
            Events.Add("hide");
            return Task.CompletedTask;
        }
    }

    private sealed class FakeGestureSettingsProvider : IGestureSettingsProvider
    {
        private readonly bool _enabled;
        private readonly int _maxDurationMs;
        private readonly bool _showOverlay;
        private readonly bool _debugEnabled;

        public FakeGestureSettingsProvider(bool enabled, int maxDurationMs, bool showOverlay, bool debugEnabled)
        {
            _enabled = enabled;
            _maxDurationMs = maxDurationMs;
            _showOverlay = showOverlay;
            _debugEnabled = debugEnabled;
        }

        public GestureSettings GetCurrent()
        {
            return new GestureSettings(
                _enabled,
                _showOverlay,
                CloseWindowEnabled: false,
                DebugEnabled: _debugEnabled,
                GesturePreset.EditEnhanced,
                new GestureOptions(20, 16, _maxDurationMs, 2));
        }

        public void Update(GestureSettings settings)
        {
        }
    }

    private sealed class FakeForegroundAppService : IForegroundAppService
    {
        public Core.SystemInfo.ForegroundAppInfo Current { get; set; } = new("test.exe", "Test");

        public Core.SystemInfo.ForegroundAppInfo GetCurrent() => Current;
    }

    private sealed class FakeAppBlacklistService : IAppBlacklistService
    {
        public bool GestureBlocked { get; set; }

        public Task<IReadOnlyList<Core.Privacy.AppBlacklistItem>> GetAllAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<Core.Privacy.AppBlacklistItem>>([]);
        }

        public Task AddAsync(string processName, bool blockClipboard, bool blockGesture, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task DeleteAsync(Guid id, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task UpdateAsync(Guid id, bool blockClipboard, bool blockGesture, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<bool> IsClipboardBlockedAsync(string? processName, CancellationToken cancellationToken) => Task.FromResult(false);

        public Task<bool> IsGestureBlockedAsync(string? processName, CancellationToken cancellationToken) => Task.FromResult(GestureBlocked);

        public Task RefreshAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public bool IsGestureBlockedCached(string? processName) => GestureBlocked;
    }
}
