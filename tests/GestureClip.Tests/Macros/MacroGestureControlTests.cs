using GestureClip.Core.Abstractions;
using GestureClip.Core.Gestures;
using GestureClip.Core.Macros;
using GestureClip.Features.Gestures;
using GestureClip.Features.Macros;
using Xunit;

namespace GestureClip.Tests.Macros;

public sealed class MacroGestureControlTests
{
    [Theory]
    [InlineData(GesturePreset.EditEnhanced)]
    [InlineData(GesturePreset.ClipboardEnhanced)]
    [InlineData(GesturePreset.Custom)]
    public void Chevron_controls_resolve_in_every_existing_preset(GesturePreset preset)
    {
        var provider = new GesturePresetProvider();
        Assert.Equal(BuiltInGestureAction.MacroToggleRecording, provider.GetAction(preset, "<"));
        Assert.Equal(BuiltInGestureAction.MacroTogglePlayback, provider.GetAction(preset, new GestureExecutionContext(">", false)));
        Assert.NotEqual(BuiltInGestureAction.MacroToggleRecording, provider.GetAction(preset, "LR"));
    }

    [Fact]
    public async Task Macro_executor_routes_control_context_and_preserves_other_actions()
    {
        var target = new RoutingProbe();
        var executor = new MacroGestureActionExecutor(target, target);
        var context = new GestureExecutionContext("<", false) { GestureStartedAt = DateTimeOffset.UtcNow };
        await executor.ExecuteAsync(BuiltInGestureAction.MacroToggleRecording, context, CancellationToken.None);
        Assert.Same(context, target.MacroContext);
        await executor.ExecuteAsync(BuiltInGestureAction.Copy, context, CancellationToken.None);
        Assert.Equal(BuiltInGestureAction.Copy, target.StandardAction);
    }

    [Fact]
    public void Control_stroke_is_removed_without_losing_keyboard_or_later_mouse_events()
    {
        var started = DateTimeOffset.UtcNow;
        MacroEvent[] events =
        [
            Event(MacroEventKind.Move, 10), Event(MacroEventKind.RightDown, 20),
            Event(MacroEventKind.Move, 30), Event(MacroEventKind.KeyDown, 40),
            Event(MacroEventKind.Move, 50), Event(MacroEventKind.RightUp, 60), Event(MacroEventKind.LeftDown, 70)
        ];
        var result = MacroControlGestureFilter.Exclude(events, started, new MacroGestureInterval(started.AddTicks(21), started.AddTicks(61)));
        Assert.Equal(new[] { events[0], events[3], events[6] }, result);
        Assert.Equal(7, events.Length);
    }

    [Fact]
    public void Already_excluded_recorder_gesture_does_not_remove_an_earlier_right_click()
    {
        var started = DateTimeOffset.UtcNow;
        MacroEvent[] events = [Event(MacroEventKind.RightDown, 10), Event(MacroEventKind.RightUp, 20)];
        var result = MacroControlGestureFilter.Exclude(events, started, new MacroGestureInterval(started.AddTicks(30), started.AddTicks(40)));
        Assert.Same(events, result);
    }

    [Fact]
    public void Overlapping_keyboard_events_are_not_retimed_by_gesture_exclusion()
    {
        var started = DateTimeOffset.UtcNow;
        MacroEvent[] events = [Event(MacroEventKind.RightDown, 10), Event(MacroEventKind.KeyDown, 20), Event(MacroEventKind.RightUp, 30)];
        var result = MacroControlGestureFilter.Exclude(events, started, new MacroGestureInterval(started.AddTicks(11), started.AddTicks(31)));
        Assert.Equal(20, Assert.Single(result).OffsetTicks);
    }

    private static MacroEvent Event(MacroEventKind kind, long ticks) => new() { Kind = kind, OffsetTicks = ticks };

    private sealed class RoutingProbe : IMouseGestureActionExecutor, IMacroGestureController
    {
        public GestureExecutionContext? MacroContext { get; private set; }
        public BuiltInGestureAction StandardAction { get; private set; }
        public Task ExecuteAsync(BuiltInGestureAction action, CancellationToken cancellationToken)
        {
            StandardAction = action;
            return Task.CompletedTask;
        }
        public Task ExecuteGestureAsync(BuiltInGestureAction action, GestureExecutionContext context, CancellationToken token)
        {
            MacroContext = context;
            return Task.CompletedTask;
        }
    }
}
