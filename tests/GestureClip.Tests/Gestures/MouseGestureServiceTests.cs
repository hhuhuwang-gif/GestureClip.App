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
        Assert.Equal((0, 0), synthesizer.Clicks.Single());
        Assert.Empty(executor.Actions);
    }

    [Fact]
    public async Task Right_click_without_threshold_does_not_show_overlay_hint()
    {
        var hook = new FakeLowLevelMouseHook();
        var synthesizer = new FakeRightClickSynthesizer();
        var overlay = new FakeGestureOverlayService();
        var service = CreateService(hook, synthesizer: synthesizer, overlay: overlay);
        await service.StartAsync(CancellationToken.None);

        hook.Raise(MouseHookEventType.RightButtonDown, 0, 0);
        hook.Raise(MouseHookEventType.RightButtonUp, 0, 0);
        await WaitForAsync(() => synthesizer.Clicks.Count == 1);

        Assert.Empty(overlay.Events);
    }

    [Fact]
    public async Task Right_click_without_threshold_synthesizes_click_at_original_down_position()
    {
        var hook = new FakeLowLevelMouseHook();
        var synthesizer = new FakeRightClickSynthesizer();
        var service = CreateService(hook, synthesizer: synthesizer, showOverlay: false);
        await service.StartAsync(CancellationToken.None);

        hook.Raise(MouseHookEventType.RightButtonDown, 100, 200);
        hook.Raise(MouseHookEventType.Move, 104, 203);
        var up = hook.Raise(MouseHookEventType.RightButtonUp, 106, 205);
        await WaitForAsync(() => synthesizer.Clicks.Count == 1);

        Assert.True(up.Suppress);
        Assert.Equal((100, 200), synthesizer.Clicks.Single());
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
    public async Task Gesture_active_records_gesture_count_after_action()
    {
        var hook = new FakeLowLevelMouseHook();
        var executor = new FakeMouseGestureActionExecutor();
        var dashboard = new FakeWorkstationDashboardService();
        var service = CreateService(hook, executor, dashboard: dashboard, showOverlay: false);
        await service.StartAsync(CancellationToken.None);

        hook.Raise(MouseHookEventType.RightButtonDown, 0, 0);
        hook.Raise(MouseHookEventType.Move, 0, -30);
        hook.Raise(MouseHookEventType.RightButtonUp, 0, -50);
        await WaitForAsync(() => executor.Actions.Count == 1);
        await WaitForAsync(() => dashboard.GestureCount == 1);

        Assert.Equal(1, dashboard.GestureCount);
    }

    [Fact]
    public async Task Slow_stats_and_worker_level_do_not_delay_overlay_hide_after_action()
    {
        var hook = new FakeLowLevelMouseHook();
        var executor = new FakeMouseGestureActionExecutor();
        var overlay = new FakeGestureOverlayService();
        var dashboard = new FakeWorkstationDashboardService { RecordGestureDelay = TimeSpan.FromMilliseconds(500) };
        var workerLevel = new FakeWorkerLevelService { RecordDelay = TimeSpan.FromMilliseconds(500) };
        var service = CreateService(
            hook,
            executor,
            overlay: overlay,
            dashboard: dashboard,
            workerLevel: workerLevel);
        await service.StartAsync(CancellationToken.None);

        hook.Raise(MouseHookEventType.RightButtonDown, 0, 0);
        hook.Raise(MouseHookEventType.Move, 0, -30);
        hook.Raise(MouseHookEventType.RightButtonUp, 0, -50);
        await WaitForAsync(() => executor.Actions.Count == 1);
        await Task.Delay(120);

        Assert.Contains("hide", overlay.Events);
        Assert.Equal(GestureRuntimeState.Idle, service.Diagnostics.State);
    }


    [Fact]
    public async Task Right_button_events_pass_through_when_right_trigger_disabled()
    {
        var hook = new FakeLowLevelMouseHook();
        var executor = new FakeMouseGestureActionExecutor();
        var service = CreateService(hook, executor, showOverlay: false, rightButtonEnabled: false);
        await service.StartAsync(CancellationToken.None);

        var down = hook.Raise(MouseHookEventType.RightButtonDown, 0, 0);
        hook.Raise(MouseHookEventType.Move, 0, -30);
        var up = hook.Raise(MouseHookEventType.RightButtonUp, 0, -50);
        await Task.Delay(50);

        Assert.False(down.Suppress);
        Assert.False(up.Suppress);
        Assert.Empty(executor.Actions);
    }
    [Fact]
    public async Task Middle_button_gesture_executes_when_enabled()
    {
        var hook = new FakeLowLevelMouseHook();
        var executor = new FakeMouseGestureActionExecutor();
        var service = CreateService(hook, executor, showOverlay: false, middleButtonEnabled: true);
        await service.StartAsync(CancellationToken.None);

        var down = hook.Raise(MouseHookEventType.MiddleButtonDown, 0, 0);
        hook.Raise(MouseHookEventType.Move, 0, -30);
        var up = hook.Raise(MouseHookEventType.MiddleButtonUp, 0, -50);
        await WaitForAsync(() => executor.Actions.Count == 1);

        Assert.True(down.Suppress);
        Assert.True(up.Suppress);
        Assert.Equal(BuiltInGestureAction.Copy, executor.Actions.Single());
    }

    [Fact]
    public async Task Middle_button_events_pass_through_when_disabled()
    {
        var hook = new FakeLowLevelMouseHook();
        var executor = new FakeMouseGestureActionExecutor();
        var service = CreateService(hook, executor, showOverlay: false, middleButtonEnabled: false);
        await service.StartAsync(CancellationToken.None);

        var down = hook.Raise(MouseHookEventType.MiddleButtonDown, 0, 0);
        hook.Raise(MouseHookEventType.Move, 0, -30);
        var up = hook.Raise(MouseHookEventType.MiddleButtonUp, 0, -50);
        await Task.Delay(50);

        Assert.False(down.Suppress);
        Assert.False(up.Suppress);
        Assert.Empty(executor.Actions);
    }

    [Fact]
    public async Task Left_button_events_pass_through_when_disabled_by_default()
    {
        var hook = new FakeLowLevelMouseHook();
        var executor = new FakeMouseGestureActionExecutor();
        var overlay = new FakeGestureOverlayService();
        var service = CreateService(hook, executor, overlay: overlay, showOverlay: false, leftButtonEnabled: false);
        await service.StartAsync(CancellationToken.None);

        var down = hook.Raise(MouseHookEventType.LeftButtonDown, 0, 0);
        hook.Raise(MouseHookEventType.Move, 0, -30);
        var up = hook.Raise(MouseHookEventType.LeftButtonUp, 0, -50);
        await Task.Delay(50);

        Assert.False(down.Suppress);
        Assert.False(up.Suppress);
        Assert.Empty(executor.Actions);
        Assert.Empty(overlay.Events);
        Assert.Equal(GestureRuntimeState.Idle, service.Diagnostics.State);
    }

    [Fact]
    public async Task Left_button_never_starts_drag_gesture_even_when_legacy_setting_is_enabled()
    {
        var hook = new FakeLowLevelMouseHook();
        var executor = new FakeMouseGestureActionExecutor();
        var service = CreateService(hook, executor, showOverlay: false, leftButtonEnabled: true);
        await service.StartAsync(CancellationToken.None);

        var down = hook.Raise(MouseHookEventType.LeftButtonDown, 0, 0);
        hook.Raise(MouseHookEventType.Move, 0, -30);
        var up = hook.Raise(MouseHookEventType.LeftButtonUp, 0, -50);
        await Task.Delay(50);

        Assert.False(down.Suppress);
        Assert.False(up.Suppress);
        Assert.Empty(executor.Actions);
        Assert.Equal(GestureRuntimeState.Idle, service.Diagnostics.State);
    }

    [Fact]
    public async Task Right_button_drag_then_left_click_marks_left_modifier_and_executes_enhanced_action()
    {
        var hook = new FakeLowLevelMouseHook();
        var executor = new FakeMouseGestureActionExecutor();
        var synthesizer = new FakeRightClickSynthesizer();
        var overlay = new FakeGestureOverlayService();
        var service = CreateService(hook, executor, synthesizer: synthesizer, overlay: overlay, leftButtonEnabled: false);
        await service.StartAsync(CancellationToken.None);

        var rightDown = hook.Raise(MouseHookEventType.RightButtonDown, 0, 0);
        hook.Raise(MouseHookEventType.Move, 0, -30);
        var leftDown = hook.Raise(MouseHookEventType.LeftButtonDown, 0, -30);
        var leftUp = hook.Raise(MouseHookEventType.LeftButtonUp, 0, -30);
        var rightUp = hook.Raise(MouseHookEventType.RightButtonUp, 0, -30);
        await WaitForAsync(() => executor.Actions.Count == 1);

        Assert.True(rightDown.Suppress);
        Assert.True(leftDown.Suppress);
        Assert.True(leftUp.Suppress);
        Assert.True(rightUp.Suppress);
        Assert.Equal(BuiltInGestureAction.SelectAll, executor.Actions.Single());
        Assert.True(executor.Contexts.Single().IsLeftButtonModified);
        Assert.Equal("U", service.Diagnostics.LastPattern);
        Assert.Empty(synthesizer.MouseClicks);
        Assert.Contains("complete:U", overlay.Events);
        Assert.Contains(overlay.HudInfos, info => info.DirectionText == "↑ + 左键" && info.ActionName == "全选");
        Assert.Contains("hide", overlay.Events);
        Assert.Equal(GestureRuntimeState.Idle, service.Diagnostics.State);
    }

    [Fact]
    public async Task Right_button_gesture_without_left_click_passes_modifier_false()
    {
        var hook = new FakeLowLevelMouseHook();
        var executor = new FakeMouseGestureActionExecutor();
        var service = CreateService(hook, executor, showOverlay: false);
        await service.StartAsync(CancellationToken.None);

        hook.Raise(MouseHookEventType.RightButtonDown, 0, 0);
        hook.Raise(MouseHookEventType.Move, 0, -30);
        hook.Raise(MouseHookEventType.RightButtonUp, 0, -50);
        await WaitForAsync(() => executor.Actions.Count == 1);

        Assert.Equal(BuiltInGestureAction.Copy, executor.Actions.Single());
        Assert.False(executor.Contexts.Single().IsLeftButtonModified);
    }

    [Fact]
    public async Task Left_modifier_resets_after_current_gesture()
    {
        var hook = new FakeLowLevelMouseHook();
        var executor = new FakeMouseGestureActionExecutor();
        var service = CreateService(hook, executor, showOverlay: false);
        await service.StartAsync(CancellationToken.None);

        hook.Raise(MouseHookEventType.RightButtonDown, 0, 0);
        hook.Raise(MouseHookEventType.Move, 0, -30);
        hook.Raise(MouseHookEventType.LeftButtonDown, 0, -30);
        hook.Raise(MouseHookEventType.LeftButtonUp, 0, -30);
        hook.Raise(MouseHookEventType.RightButtonUp, 0, -30);
        await WaitForAsync(() => executor.Actions.Count == 1);

        hook.Raise(MouseHookEventType.RightButtonDown, 20, 20);
        hook.Raise(MouseHookEventType.Move, 20, -10);
        hook.Raise(MouseHookEventType.RightButtonUp, 20, -30);
        await WaitForAsync(() => executor.Actions.Count == 2);

        Assert.Equal([BuiltInGestureAction.SelectAll, BuiltInGestureAction.Copy], executor.Actions.ToArray());
        Assert.Equal([true, false], executor.Contexts.Select(context => context.IsLeftButtonModified).ToArray());
    }

    [Fact]
    public async Task Right_button_held_left_click_executes_left_rocker_gesture_without_enabling_left_drag_gesture()
    {
        var hook = new FakeLowLevelMouseHook();
        var executor = new FakeMouseGestureActionExecutor();
        var preset = new FakeGesturePresetProvider(new Dictionary<string, BuiltInGestureAction>(StringComparer.Ordinal)
        {
            ["R+L"] = BuiltInGestureAction.PasteAndEnter
        });
        var service = CreateService(hook, executor, showOverlay: false, leftButtonEnabled: false, presetProvider: preset);
        await service.StartAsync(CancellationToken.None);

        var rightDown = hook.Raise(MouseHookEventType.RightButtonDown, 10, 10);
        var leftDown = hook.Raise(MouseHookEventType.LeftButtonDown, 10, 10);
        var leftUp = hook.Raise(MouseHookEventType.LeftButtonUp, 10, 10);
        var rightUp = hook.Raise(MouseHookEventType.RightButtonUp, 10, 10);
        await WaitForAsync(() => executor.Actions.Count == 1);

        Assert.True(rightDown.Suppress);
        Assert.True(leftDown.Suppress);
        Assert.True(leftUp.Suppress);
        Assert.True(rightUp.Suppress);
        Assert.Equal("R+L", service.Diagnostics.LastPattern);
        Assert.Equal(BuiltInGestureAction.PasteAndEnter, executor.Actions.Single());
        Assert.Equal(GestureRuntimeState.Idle, service.Diagnostics.State);
    }


    [Fact]
    public async Task Right_left_rocker_suppresses_buttons_when_right_is_released_first()
    {
        var hook = new FakeLowLevelMouseHook();
        var executor = new FakeMouseGestureActionExecutor();
        var preset = new FakeGesturePresetProvider(new Dictionary<string, BuiltInGestureAction>(StringComparer.Ordinal)
        {
            ["R+L"] = BuiltInGestureAction.PasteAndEnter
        });
        var service = CreateService(hook, executor, showOverlay: false, presetProvider: preset);
        await service.StartAsync(CancellationToken.None);

        var rightDown = hook.Raise(MouseHookEventType.RightButtonDown, 10, 10);
        var leftDown = hook.Raise(MouseHookEventType.LeftButtonDown, 10, 10);
        var rightUp = hook.Raise(MouseHookEventType.RightButtonUp, 10, 10);
        var leftUp = hook.Raise(MouseHookEventType.LeftButtonUp, 10, 10);
        await WaitForAsync(() => executor.Actions.Count == 1);

        Assert.True(rightDown.Suppress);
        Assert.True(leftDown.Suppress);
        Assert.True(rightUp.Suppress);
        Assert.True(leftUp.Suppress);
        Assert.Equal(BuiltInGestureAction.PasteAndEnter, executor.Actions.Single());
        Assert.Equal(GestureRuntimeState.Idle, service.Diagnostics.State);
    }

    [Fact]
    public async Task XButton1_light_click_synthesizes_original_click_when_enabled()
    {
        var hook = new FakeLowLevelMouseHook();
        var synthesizer = new FakeRightClickSynthesizer();
        var service = CreateService(hook, synthesizer: synthesizer, showOverlay: false, xButton1Enabled: true);
        await service.StartAsync(CancellationToken.None);

        var down = hook.Raise(MouseHookEventType.XButton1Down, 10, 10);
        var up = hook.Raise(MouseHookEventType.XButton1Up, 12, 12);
        await WaitForAsync(() => synthesizer.MouseClicks.Count == 1);

        Assert.True(down.Suppress);
        Assert.True(up.Suppress);
        Assert.Equal((GestureTriggerButton.XButton1, 10, 10), synthesizer.MouseClicks.Single());
    }

    [Fact]
    public async Task XButton2_gesture_executes_when_enabled()
    {
        var hook = new FakeLowLevelMouseHook();
        var executor = new FakeMouseGestureActionExecutor();
        var service = CreateService(hook, executor, showOverlay: false, xButton2Enabled: true);
        await service.StartAsync(CancellationToken.None);

        var down = hook.Raise(MouseHookEventType.XButton2Down, 0, 0);
        hook.Raise(MouseHookEventType.Move, 0, -30);
        var up = hook.Raise(MouseHookEventType.XButton2Up, 0, -50);
        await WaitForAsync(() => executor.Actions.Count == 1);

        Assert.True(down.Suppress);
        Assert.True(up.Suppress);
        Assert.Equal(BuiltInGestureAction.Copy, executor.Actions.Single());
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
    public async Task Overlay_starts_only_after_trigger_threshold_is_crossed()
    {
        var hook = new FakeLowLevelMouseHook();
        var overlay = new FakeGestureOverlayService();
        var service = CreateService(hook, overlay: overlay);
        await service.StartAsync(CancellationToken.None);

        hook.Raise(MouseHookEventType.RightButtonDown, 10, 10);
        hook.Raise(MouseHookEventType.Move, 14, 14);

        Assert.Empty(overlay.Events);

        hook.Raise(MouseHookEventType.Move, 10, -20);

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
        Assert.Equal((0, 0), synthesizer.Clicks.Single());
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
        hook.Raise(MouseHookEventType.Move, 80, 40);
        hook.Raise(MouseHookEventType.RightButtonUp, 80, 40);

        await WaitForAsync(() => overlay.Events.Contains("hide"));

        Assert.Empty(executor.Actions);
        Assert.Contains(overlay.HudInfos, info => info.Pattern == "RDR" && info.ActionName == "未绑定");
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
        Assert.Contains(overlay.PointSnapshots, points => points.Count == 96 && points[0] != new GesturePoint(100, 100, points[0].Time));
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
        hook.Raise(MouseHookEventType.Move, 120, 60);
        await WaitForAsync(() => overlay.Events.Any(entry => entry.StartsWith("update:", StringComparison.Ordinal)));

        Assert.DoesNotContain("hide", overlay.Events);

        var up = hook.Raise(MouseHookEventType.RightButtonUp, 120, 60);
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
        hook.Raise(MouseHookEventType.Move, 120, 60);
        hook.Raise(MouseHookEventType.RightButtonUp, 120, 60);

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
        Assert.Equal((10, 10), synthesizer.Clicks.Single());
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

    [Fact]
    public async Task Gesture_action_success_records_worker_level_xp()
    {
        var hook = new FakeLowLevelMouseHook();
        var executor = new FakeMouseGestureActionExecutor();
        var workerLevel = new FakeWorkerLevelService();
        var service = CreateService(hook, executor, showOverlay: false, workerLevel: workerLevel);
        await service.StartAsync(CancellationToken.None);

        hook.Raise(MouseHookEventType.RightButtonDown, 0, 0);
        hook.Raise(MouseHookEventType.Move, 0, -30);
        hook.Raise(MouseHookEventType.RightButtonUp, 0, -50);
        await WaitForAsync(() => workerLevel.RecordedActions.Count == 1);

        Assert.Equal(BuiltInGestureAction.Copy, workerLevel.RecordedActions.Single());
        Assert.True(workerLevel.LastGestureSuccess);
        Assert.Equal(GestureRuntimeState.Idle, service.Diagnostics.State);
    }

    [Fact]
    public async Task Worker_level_exception_does_not_break_gesture_state()
    {
        var hook = new FakeLowLevelMouseHook();
        var executor = new FakeMouseGestureActionExecutor();
        var workerLevel = new FakeWorkerLevelService { ThrowOnRecord = true };
        var service = CreateService(hook, executor, showOverlay: false, workerLevel: workerLevel);
        await service.StartAsync(CancellationToken.None);

        hook.Raise(MouseHookEventType.RightButtonDown, 0, 0);
        hook.Raise(MouseHookEventType.Move, 0, -30);
        hook.Raise(MouseHookEventType.RightButtonUp, 0, -50);
        await WaitForAsync(() => executor.Actions.Count == 1);

        Assert.Equal(BuiltInGestureAction.Copy, executor.Actions.Single());
        Assert.Equal(GestureRuntimeState.Idle, service.Diagnostics.State);
    }
    [Fact]
    public async Task Gesture_level_up_requests_level_up_popup_after_action()
    {
        var hook = new FakeLowLevelMouseHook();
        var executor = new FakeMouseGestureActionExecutor();
        var workerLevel = new FakeWorkerLevelService { LeveledUp = true };
        var levelUp = new FakeWorkerLevelUpService();
        var service = CreateService(hook, executor, showOverlay: false, workerLevel: workerLevel, levelUp: levelUp);
        await service.StartAsync(CancellationToken.None);

        hook.Raise(MouseHookEventType.RightButtonDown, 0, 0);
        hook.Raise(MouseHookEventType.Move, 0, -30);
        hook.Raise(MouseHookEventType.RightButtonUp, 0, -50);
        await WaitForAsync(() => levelUp.ShowCount == 1);

        Assert.Equal(1, levelUp.ShowCount);
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
        bool gestureBlacklisted = false,
        bool leftButtonEnabled = false,
        bool rightButtonEnabled = true,
        bool middleButtonEnabled = false,
        bool xButton1Enabled = false,
        bool xButton2Enabled = false,
        FakeWorkstationDashboardService? dashboard = null,
        FakeWorkerLevelService? workerLevel = null,
        FakeWorkerLevelUpService? levelUp = null,
        IGesturePresetProvider? presetProvider = null)
    {
        return new MouseGestureService(
            hook,
            new DirectionGestureRecognizer(),
            executor ?? new FakeMouseGestureActionExecutor(),
            synthesizer ?? new FakeRightClickSynthesizer(),
            new FakeGestureSettingsProvider(enabled, maxDurationMs, showOverlay, debugEnabled, leftButtonEnabled, rightButtonEnabled, middleButtonEnabled, xButton1Enabled, xButton2Enabled),
            presetProvider ?? new GesturePresetProvider(),
            new GestureHudInfoProvider(presetProvider ?? new GesturePresetProvider()),
            overlay ?? new FakeGestureOverlayService(),
            new FakeForegroundAppService(),
            new FakeAppBlacklistService { GestureBlocked = gestureBlacklisted },
            dashboard ?? new FakeWorkstationDashboardService(),
            workerLevel ?? new FakeWorkerLevelService(),
            levelUp ?? new FakeWorkerLevelUpService(),
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
        public List<(GestureTriggerButton Button, int X, int Y)> MouseClicks { get; } = [];

        public void SynthesizeRightClick(int x, int y)
        {
            SynthesizeClick(GestureTriggerButton.Right, x, y);
        }

        public void SynthesizeClick(GestureTriggerButton button, int x, int y)
        {
            MouseClicks.Add((button, x, y));
            if (button == GestureTriggerButton.Right)
            {
                Clicks.Add((x, y));
            }
        }

        public void SynthesizeWheel(int delta, int x, int y)
        {
        }
    }

    private sealed class FakeMouseGestureActionExecutor : IMouseGestureActionExecutor
    {
        public ConcurrentQueue<BuiltInGestureAction> Actions { get; } = [];
        public ConcurrentQueue<GestureExecutionContext> Contexts { get; } = [];

        public Task ExecuteAsync(BuiltInGestureAction action, CancellationToken cancellationToken)
        {
            Actions.Enqueue(action);
            return Task.CompletedTask;
        }

        public Task ExecuteAsync(BuiltInGestureAction action, GestureExecutionContext context, CancellationToken cancellationToken)
        {
            Actions.Enqueue(action);
            Contexts.Enqueue(context);
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

        public List<IReadOnlyList<GesturePoint>> PointSnapshots { get; } = [];

        public Task ShowGestureStartAsync(GesturePoint point, GestureHudInfo hudInfo, CancellationToken cancellationToken)
        {
            HudInfos.Add(hudInfo);
            PointCounts.Add(1);
            PointSnapshots.Add([point]);
            Events.Add($"start:{point.X},{point.Y}");
            return Task.CompletedTask;
        }

        public Task UpdateGestureAsync(IReadOnlyList<GesturePoint> points, GestureHudInfo hudInfo, CancellationToken cancellationToken)
        {
            HudInfos.Add(hudInfo);
            PointCounts.Add(points.Count);
            PointSnapshots.Add([.. points]);
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
        private readonly bool _leftButtonEnabled;
        private readonly bool _rightButtonEnabled;
        private readonly bool _middleButtonEnabled;
        private readonly bool _xButton1Enabled;
        private readonly bool _xButton2Enabled;

        public FakeGestureSettingsProvider(bool enabled, int maxDurationMs, bool showOverlay, bool debugEnabled, bool leftButtonEnabled, bool rightButtonEnabled, bool middleButtonEnabled, bool xButton1Enabled, bool xButton2Enabled)
        {
            _enabled = enabled;
            _maxDurationMs = maxDurationMs;
            _showOverlay = showOverlay;
            _debugEnabled = debugEnabled;
            _leftButtonEnabled = leftButtonEnabled;
            _rightButtonEnabled = rightButtonEnabled;
            _middleButtonEnabled = middleButtonEnabled;
            _xButton1Enabled = xButton1Enabled;
            _xButton2Enabled = xButton2Enabled;
        }

        public GestureSettings GetCurrent()
        {
            return new GestureSettings(
                _enabled,
                _showOverlay,
                CloseWindowEnabled: false,
                DebugEnabled: _debugEnabled,
                GesturePreset.EditEnhanced,
                new GestureOptions(20, 16, _maxDurationMs, 2),
                _leftButtonEnabled,
                _middleButtonEnabled,
                _xButton1Enabled,
                _xButton2Enabled,
                _rightButtonEnabled);
        }

        public void Update(GestureSettings settings)
        {
        }
    }

    private sealed class FakeGesturePresetProvider : IGesturePresetProvider
    {
        private readonly IReadOnlyDictionary<string, BuiltInGestureAction> _bindings;

        public FakeGesturePresetProvider(IReadOnlyDictionary<string, BuiltInGestureAction> bindings)
        {
            _bindings = bindings;
        }

        public BuiltInGestureAction GetAction(GesturePreset preset, string pattern)
        {
            return _bindings.TryGetValue(pattern, out var action) ? action : BuiltInGestureAction.None;
        }

        public IReadOnlyDictionary<string, BuiltInGestureAction> GetBindings(GesturePreset preset) => _bindings;

        public void UpdateCustomBindings(IReadOnlyDictionary<string, BuiltInGestureAction> bindings)
        {
        }

        public IReadOnlyDictionary<string, BuiltInGestureAction> GetLeftButtonEnhancedBindings() =>
            new Dictionary<string, BuiltInGestureAction>(StringComparer.Ordinal);

        public void UpdateLeftButtonEnhancedBindings(IReadOnlyDictionary<string, BuiltInGestureAction> bindings)
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

    private sealed class FakeWorkstationDashboardService : IWorkstationDashboardService
    {
        public int GestureCount { get; private set; }

        public TimeSpan RecordGestureDelay { get; init; }

        public Task<Core.Workstation.WorkstationDashboardSnapshot> GetSnapshotAsync(DateTimeOffset now, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task StartFishingAsync(DateTimeOffset now, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task EndFishingAsync(DateTimeOffset now, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task ResetTodayAsync(DateOnly date, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task RecordCopyAsync(DateTimeOffset now, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task RecordPasteAsync(DateTimeOffset now, CancellationToken cancellationToken) => Task.CompletedTask;

        public async Task RecordGestureAsync(DateTimeOffset now, CancellationToken cancellationToken)
        {
            if (RecordGestureDelay > TimeSpan.Zero)
            {
                await Task.Delay(RecordGestureDelay, cancellationToken);
            }

            GestureCount++;
        }
    }
    private sealed class FakeWorkerLevelUpService : IWorkerLevelUpService
    {
        public int ShowCount { get; private set; }

        public Task ShowLevelUpAsync(Core.WorkerLevel.WorkerLevelSnapshot snapshot, CancellationToken cancellationToken)
        {
            ShowCount++;
            return Task.CompletedTask;
        }
    }
    private sealed class FakeWorkerLevelService : IWorkerLevelService
    {
        public List<BuiltInGestureAction> RecordedActions { get; } = [];

        public bool LastGestureSuccess { get; private set; }

        public bool ThrowOnRecord { get; set; }

        public bool LeveledUp { get; set; }

        public TimeSpan RecordDelay { get; init; }

        public Task<Core.WorkerLevel.WorkerLevelSnapshot> GetSnapshotAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(new Core.WorkerLevel.WorkerLevelSnapshot(
                0,
                0,
                new Core.WorkerLevel.WorkerLevelDefinition(1, 0, "初入工位"),
                new Core.WorkerLevel.WorkerLevelDefinition(2, 50, "复制学徒"),
                0,
                50,
                0,
                LeveledUp,
                1,
                null));
        }

        public Task<Core.WorkerLevel.WorkerLevelSnapshot> RecordBonusXpAsync(int xp, DateTimeOffset now, CancellationToken cancellationToken)
        {
            return GetSnapshotAsync(cancellationToken);
        }

        public Task<Core.WorkerLevel.WorkerLevelSnapshot> RecordActionAsync(BuiltInGestureAction action, bool isGestureSuccess, DateTimeOffset now, CancellationToken cancellationToken)
        {
            if (ThrowOnRecord)
            {
                throw new InvalidOperationException("xp boom");
            }

            if (RecordDelay > TimeSpan.Zero)
            {
                return RecordActionWithDelayAsync(action, isGestureSuccess, cancellationToken);
            }

            RecordedActions.Add(action);
            LastGestureSuccess = isGestureSuccess;
            return GetSnapshotAsync(cancellationToken);
        }

        private async Task<Core.WorkerLevel.WorkerLevelSnapshot> RecordActionWithDelayAsync(
            BuiltInGestureAction action,
            bool isGestureSuccess,
            CancellationToken cancellationToken)
        {
            await Task.Delay(RecordDelay, cancellationToken);
            RecordedActions.Add(action);
            LastGestureSuccess = isGestureSuccess;
            return await GetSnapshotAsync(cancellationToken);
        }
    }
}




