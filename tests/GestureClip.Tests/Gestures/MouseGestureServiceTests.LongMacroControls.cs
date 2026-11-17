using GestureClip.Core.Gestures;
using Xunit;

namespace GestureClip.Tests.Gestures;

public sealed partial class MouseGestureServiceTests
{
    [Theory]
    [InlineData(false, BuiltInGestureAction.MacroToggleRecording)]
    [InlineData(true, BuiltInGestureAction.MacroTogglePlayback)]
    public async Task Long_chevrons_keep_their_first_leg_when_sample_history_fills(bool mirror, BuiltInGestureAction expected)
    {
        const int legSteps = 100;
        const int coordinateStep = 20;
        const int eventIntervalMs = 5;
        var hook = new FakeLowLevelMouseHook();
        var overlay = new FakeGestureOverlayService();
        var executor = new FakeMouseGestureActionExecutor();
        var service = CreateService(hook, executor, overlay: overlay);
        await service.StartAsync(CancellationToken.None);
        try
        {
            var start = DateTimeOffset.UtcNow;
            var initialX = mirror ? -legSteps * coordinateStep : legSteps * coordinateStep;
            hook.Raise(MouseHookEventType.RightButtonDown, initialX, 0, time: start);
            for (var step = 1; step <= legSteps * 2; step++)
            {
                var x = Math.Abs(legSteps - step) * coordinateStep;
                hook.Raise(MouseHookEventType.Move, mirror ? -x : x, step * coordinateStep,
                    time: start.AddMilliseconds(step * eventIntervalMs));
            }
            hook.Raise(MouseHookEventType.RightButtonUp, initialX, legSteps * 2 * coordinateStep,
                time: start.AddMilliseconds(legSteps * 2 * eventIntervalMs + 1));
            await WaitForAsync(() => service.Diagnostics.LastPattern is not null);
            Assert.Equal(mirror ? ">" : "<", service.Diagnostics.LastPattern);
            await WaitForAsync(() => executor.Actions.Count == 1);
            Assert.Equal(expected, Assert.Single(executor.Actions));
            Assert.All(overlay.PointCounts, count => Assert.InRange(count, 1, 96));
        }
        finally { await service.StopAsync(CancellationToken.None); }
    }
}
