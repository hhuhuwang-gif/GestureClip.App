using GestureClip.Core.Gestures;
using Xunit;

namespace GestureClip.Tests.Gestures;

public sealed partial class MouseGestureServiceTests
{
    [Theory]
    [InlineData(false, BuiltInGestureAction.MacroToggleRecording)]
    [InlineData(true, BuiltInGestureAction.MacroTogglePlayback)]
    public async Task Chevron_controls_keep_real_button_up_time_after_the_pointer_stops(bool pointsRight, BuiltInGestureAction action)
    {
        var hook = new FakeLowLevelMouseHook();
        var executor = new FakeMouseGestureActionExecutor();
        var service = CreateService(hook, executor, showOverlay: false);
        var started = DateTimeOffset.UtcNow;
        await service.StartAsync(CancellationToken.None);
        try
        {
            hook.Raise(MouseHookEventType.RightButtonDown, pointsRight ? 0 : 80, 0, time: started);
            for (var step = 1; step <= 8; step++)
            {
                var x = step <= 4 ? step * 20 : (8 - step) * 20;
                hook.Raise(MouseHookEventType.Move, pointsRight ? x : 80 - x, step * 20, time: started.AddMilliseconds(step * 20));
            }
            var ended = started.AddMilliseconds(600);
            hook.Raise(MouseHookEventType.RightButtonUp, pointsRight ? 0 : 80, 160, time: ended);
            await WaitForAsync(() => !executor.Contexts.IsEmpty);
            Assert.Equal(action, Assert.Single(executor.Actions));
            var context = Assert.Single(executor.Contexts);
            Assert.Equal(started, context.GestureStartedAt);
            Assert.Equal(ended, context.GestureEndedAt);
        }
        finally { await service.StopAsync(CancellationToken.None); }
    }

    [Theory]
    [InlineData(16, 11, false)]
    [InlineData(11, 10, false)]
    [InlineData(11, 10, true)]
    public async Task Fast_compact_chevron_uses_the_raw_turn_and_release_position(int horizontal, int vertical, bool finalMove)
    {
        var hook = new FakeLowLevelMouseHook();
        var executor = new FakeMouseGestureActionExecutor();
        var service = CreateService(hook, executor, showOverlay: false);
        await service.StartAsync(CancellationToken.None);
        try
        {
            var started = DateTimeOffset.UtcNow;
            hook.Raise(MouseHookEventType.RightButtonDown, horizontal, 0, time: started);
            hook.Raise(MouseHookEventType.Move, 0, vertical, time: started.AddMilliseconds(5));
            if (finalMove)
                hook.Raise(MouseHookEventType.Move, horizontal, vertical * 2, time: started.AddMilliseconds(10));
            hook.Raise(MouseHookEventType.RightButtonUp, horizontal, vertical * 2, time: started.AddMilliseconds(12));

            await WaitForAsync(() => !executor.Contexts.IsEmpty);
            Assert.Equal(BuiltInGestureAction.MacroToggleRecording, Assert.Single(executor.Actions));
        }
        finally { await service.StopAsync(CancellationToken.None); }
    }

    [Fact]
    public async Task Recording_starts_while_the_overlay_is_still_closing()
    {
        var hidden = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var overlay = new FakeGestureOverlayService { HideCompletion = hidden.Task };
        var hook = new FakeLowLevelMouseHook();
        var executor = new FakeMouseGestureActionExecutor();
        var service = CreateService(hook, executor, overlay: overlay);
        await service.StartAsync(CancellationToken.None);
        try
        {
            var start = DateTimeOffset.UtcNow;
            hook.Raise(MouseHookEventType.RightButtonDown, 60, 0, time: start);
            hook.Raise(MouseHookEventType.Move, 0, 60, time: start.AddMilliseconds(10));
            hook.Raise(MouseHookEventType.RightButtonUp, 60, 120, time: start.AddMilliseconds(20));
            await WaitForAsync(() => !executor.Contexts.IsEmpty && overlay.Events.Contains("hide"));
            Assert.Equal(BuiltInGestureAction.MacroToggleRecording, Assert.Single(executor.Actions));
            Assert.False(hidden.Task.IsCompleted);
        }
        finally
        {
            hidden.SetResult();
            await service.StopAsync(CancellationToken.None);
        }
    }
}
