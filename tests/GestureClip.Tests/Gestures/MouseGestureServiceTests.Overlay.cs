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
    public async Task High_frequency_moves_are_coalesced_and_trace_is_bounded()
    {
        var hook = new FakeLowLevelMouseHook();
        var executor = new FakeMouseGestureActionExecutor();
        var overlay = new FakeGestureOverlayService();
        var service = CreateService(hook, executor, overlay: overlay);
        await service.StartAsync(CancellationToken.None);

        var startedAt = DateTimeOffset.UtcNow;
        hook.Raise(MouseHookEventType.RightButtonDown, 100, 500, time: startedAt);
        for (var index = 1; index <= 120; index++)
        {
            hook.Raise(
                MouseHookEventType.Move,
                100,
                500 - (index * 17),
                time: startedAt.AddMilliseconds(index * 2));
        }

        hook.Raise(MouseHookEventType.RightButtonUp, 100, -1560, time: startedAt.AddMilliseconds(245));
        await WaitForAsync(() => executor.Actions.Count == 1);

        var updateCount = overlay.Events.Count(item => item.StartsWith("update:", StringComparison.Ordinal));
        Assert.InRange(updateCount, 1, 12);
        Assert.All(overlay.PointCounts, count => Assert.InRange(count, 1, 96));
        Assert.Equal(BuiltInGestureAction.Copy, executor.Actions.Single());
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
    public async Task Invalid_or_unbound_gesture_shows_unbound_hud_without_executing_action()
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

        // Unbound strokes show brief failure feedback; the real overlay owns its auto-hide timer.
        await WaitForAsync(() => overlay.Events.Any(e => e.StartsWith("complete:", StringComparison.Ordinal)));

        Assert.Empty(executor.Actions);
        Assert.DoesNotContain("hide", overlay.Events);
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

        var startedAt = DateTimeOffset.UtcNow;
        hook.Raise(MouseHookEventType.RightButtonDown, 100, 100, time: startedAt);
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
            hook.Raise(MouseHookEventType.Move, x, y, time: startedAt.AddMilliseconds(i * 2));
        }

        hook.Raise(MouseHookEventType.RightButtonUp, 120, 120, time: startedAt.AddMilliseconds(405));
        await WaitForAsync(() => overlay.Events.Contains("hide"));

        Assert.Empty(executor.Actions);
        Assert.Equal(GestureRuntimeState.Idle, service.Diagnostics.State);
        Assert.All(overlay.PointCounts, count => Assert.InRange(count, 1, 96));
        Assert.Contains(overlay.PointSnapshots, points => points.Count == 96 && (points[0].X != 100 || points[0].Y != 100));
    }

    [Fact]
    public async Task Complex_gesture_stays_visible_while_dragging_and_completes_on_release()
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
        // RDR is unbound: complete stays visible for bind CTA; hide is deferred.
        await WaitForAsync(() => overlay.Events.Any(e => e.StartsWith("complete:", StringComparison.Ordinal)));

        Assert.True(down.Suppress);
        Assert.True(up.Suppress);
        Assert.Empty(executor.Actions);
        Assert.Empty(synthesizer.Clicks);
        Assert.DoesNotContain("hide", overlay.Events);
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
}
