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
}
