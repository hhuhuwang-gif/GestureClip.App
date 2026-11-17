using GestureClip.Core.Gestures;
using Xunit;

namespace GestureClip.Tests.Gestures;

public sealed partial class MouseGestureServiceTests
{
    [Fact]
    public async Task Subsegment_pointer_movement_updates_the_trail_endpoint()
    {
        var hook = new FakeLowLevelMouseHook();
        var overlay = new FakeGestureOverlayService();
        var service = CreateService(hook, overlay: overlay);
        await service.StartAsync(CancellationToken.None);
        try
        {
            var started = DateTimeOffset.UtcNow;
            hook.Raise(MouseHookEventType.RightButtonDown, 100, 100, time: started);
            hook.Raise(MouseHookEventType.Move, 100, 70, time: started.AddMilliseconds(30));
            hook.Raise(MouseHookEventType.Move, 103, 67, time: started.AddMilliseconds(60));

            var endpoint = overlay.PointSnapshots.Last()[^1];
            Assert.Equal(103, endpoint.X);
            Assert.Equal(67, endpoint.Y);
        }
        finally { await service.StopAsync(CancellationToken.None); }
    }

    [Fact]
    public async Task Smooth_trail_tail_keeps_point_budget_and_recognition_samples_independent()
    {
        var hook = new FakeLowLevelMouseHook();
        var overlay = new FakeGestureOverlayService();
        var executor = new FakeMouseGestureActionExecutor();
        var service = CreateService(hook, executor, overlay: overlay);
        await service.StartAsync(CancellationToken.None);
        try
        {
            var started = DateTimeOffset.UtcNow;
            hook.Raise(MouseHookEventType.RightButtonDown, 100, 2000, time: started);
            for (var step = 1; step <= 100; step++)
                hook.Raise(MouseHookEventType.Move, 100, 2000 - step * 20, time: started.AddMilliseconds(step * 5));
            hook.Raise(MouseHookEventType.Move, 103, -3, time: started.AddMilliseconds(550));
            Assert.Equal(-3, overlay.PointSnapshots.Last()[^1].Y);
            Assert.All(overlay.PointCounts, count => Assert.InRange(count, 1, 96));
            hook.Raise(MouseHookEventType.RightButtonUp, 103, -3, time: started.AddMilliseconds(600));
            await WaitForAsync(() => executor.Actions.Count == 1);
            Assert.Equal(BuiltInGestureAction.Copy, Assert.Single(executor.Actions));
        }
        finally { await service.StopAsync(CancellationToken.None); }
    }
}
