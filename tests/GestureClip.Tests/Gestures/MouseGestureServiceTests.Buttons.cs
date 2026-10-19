using GestureClip.Core.Abstractions;
using GestureClip.Core.Gestures;
using GestureClip.Features.Gestures;
using Microsoft.Extensions.Logging.Abstractions;
using System.Collections.Concurrent;
using Xunit;

namespace GestureClip.Tests.Gestures;

public sealed partial class MouseGestureServiceTests
{
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
}
