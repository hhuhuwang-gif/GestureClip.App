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
    public async Task Wheel_while_holding_right_button_fires_wheel_actions_and_release_stays_quiet()
    {
        var hook = new FakeLowLevelMouseHook();
        var executor = new FakeMouseGestureActionExecutor();
        var synthesizer = new FakeRightClickSynthesizer();
        var service = CreateService(hook, executor, synthesizer: synthesizer, showOverlay: false);
        await service.StartAsync(CancellationToken.None);

        hook.Raise(MouseHookEventType.RightButtonDown, 0, 0);
        var wheelUp = hook.Raise(MouseHookEventType.Wheel, 0, 0, wheelDelta: 120);
        var wheelDown = hook.Raise(MouseHookEventType.Wheel, 0, 0, wheelDelta: -120);
        var up = hook.Raise(MouseHookEventType.RightButtonUp, 0, 0);
        await WaitForAsync(() => executor.Actions.Count == 2);

        Assert.True(wheelUp.Suppress);
        Assert.True(wheelDown.Suppress);
        Assert.True(up.Suppress);
        Assert.Contains(BuiltInGestureAction.PreviousTab, executor.Actions);
        Assert.Contains(BuiltInGestureAction.NextTab, executor.Actions);
        // Release after wheel usage must not synthesize a right click (no context menu).
        await Task.Delay(80);
        Assert.Empty(synthesizer.Clicks);
        Assert.Equal(GestureRuntimeState.Idle, service.Diagnostics.State);
    }

    [Fact]
    public async Task Wheel_without_holding_trigger_button_is_ignored()
    {
        var hook = new FakeLowLevelMouseHook();
        var executor = new FakeMouseGestureActionExecutor();
        var service = CreateService(hook, executor, showOverlay: false);
        await service.StartAsync(CancellationToken.None);

        var wheel = hook.Raise(MouseHookEventType.Wheel, 0, 0, wheelDelta: 120);
        await Task.Delay(50);

        Assert.False(wheel.Suppress);
        Assert.Empty(executor.Actions);
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
}
