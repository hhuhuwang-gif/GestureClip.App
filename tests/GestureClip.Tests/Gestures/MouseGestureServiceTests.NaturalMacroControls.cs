using GestureClip.Core.Gestures;
using Xunit;

namespace GestureClip.Tests.Gestures;

public sealed partial class MouseGestureServiceTests
{
    [Theory]
    [MemberData(nameof(NaturalChevronGestureRecognizerTests.NaturalStrokes), MemberType = typeof(NaturalChevronGestureRecognizerTests))]
    public async Task Natural_chevrons_route_macro_controls_after_service_sampling((int X, int Y)[] coordinates)
    {
        foreach (var mirror in new[] { false, true })
            foreach (var reverse in new[] { false, true })
                await AssertNaturalMacroControlAsync(coordinates, mirror, reverse);
    }

    private static async Task AssertNaturalMacroControlAsync((int X, int Y)[] coordinates, bool mirror, bool reverse)
    {
        var hook = new FakeLowLevelMouseHook();
        var executor = new FakeMouseGestureActionExecutor();
        var service = CreateService(hook, executor, showOverlay: false);
        await service.StartAsync(CancellationToken.None);
        try
        {
            var ordered = (reverse ? coordinates.Reverse() : coordinates)
                .Select(point => (X: mirror ? -point.X : point.X, point.Y)).ToArray();
            var started = DateTimeOffset.UtcNow;
            hook.Raise(MouseHookEventType.RightButtonDown, ordered[0].X, ordered[0].Y, time: started);
            for (var index = 1; index < ordered.Length; index++)
                hook.Raise(MouseHookEventType.Move, ordered[index].X, ordered[index].Y,
                    time: started.AddMilliseconds(index * 30));
            var ended = started.AddMilliseconds(ordered.Length * 30);
            hook.Raise(MouseHookEventType.RightButtonUp, ordered[^1].X, ordered[^1].Y, time: ended);
            await WaitForAsync(() => service.Diagnostics.LastPattern is not null);
            Assert.Equal(mirror ? ">" : "<", service.Diagnostics.LastPattern);
            await WaitForAsync(() => executor.Actions.Count == 1);
            Assert.Equal(mirror ? BuiltInGestureAction.MacroTogglePlayback : BuiltInGestureAction.MacroToggleRecording,
                Assert.Single(executor.Actions));
            Assert.Equal(ended, Assert.Single(executor.Contexts).GestureEndedAt);
        }
        finally { await service.StopAsync(CancellationToken.None); }
    }
}
