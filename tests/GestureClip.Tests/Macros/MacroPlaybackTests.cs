using GestureClip.Core.Abstractions;
using GestureClip.Core.Macros;
using GestureClip.Features.Macros;
using GestureClip.Infrastructure.Macros;
using GestureClip.Infrastructure.Paths;
using Xunit;

namespace GestureClip.Tests.Macros;

public sealed class MacroPlaybackTests
{
    [Fact]
    public async Task Replays_in_order_and_releases_an_unfinished_press()
    {
        var sink = new RecordingSink();
        var down = new MacroEvent { Kind = MacroEventKind.KeyDown, Code = 65, ScanCode = 30 };
        await new MacroPlayback(sink).RunAsync(new RecordedMacro { Events = [down] }, CancellationToken.None);
        Assert.Equal(new[] { MacroEventKind.KeyDown, MacroEventKind.KeyUp }, sink.Events.Select(item => item.Kind));
        Assert.Equal(down.ScanCode, sink.Events[1].ScanCode);
    }

    [Fact]
    public async Task Cancellation_releases_keys_and_mouse_without_moving_the_cursor()
    {
        using var cancellation = new CancellationTokenSource();
        var sink = new RecordingSink { OnSend = input => { if (input.Kind == MacroEventKind.LeftDown) cancellation.Cancel(); } };
        var macro = new RecordedMacro { Events =
        [
            new MacroEvent { Kind = MacroEventKind.KeyDown, Code = 65 },
            new MacroEvent { Kind = MacroEventKind.LeftDown, X = 10, Y = 20 },
            new MacroEvent { Kind = MacroEventKind.Move, X = 50, Y = 60, OffsetTicks = TimeSpan.TicksPerSecond }
        ] };
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new MacroPlayback(sink).RunAsync(macro, cancellation.Token));
        Assert.Contains(sink.Events, item => item.Kind == MacroEventKind.KeyUp);
        Assert.Contains(sink.Events, item => item.Kind == MacroEventKind.LeftUp && item.KeepCursorPosition);
        Assert.DoesNotContain(sink.Events, item => item.Kind == MacroEventKind.Move);
    }

    [Fact]
    public async Task Input_failure_is_visible_and_prior_keys_are_released()
    {
        var sink = new RecordingSink { OnSend = input =>
        {
            if (input.Kind == MacroEventKind.Move) throw new IOException("injection failed");
        } };
        var macro = new RecordedMacro { Events =
        [new MacroEvent { Kind = MacroEventKind.KeyDown, Code = 65 }, new MacroEvent { Kind = MacroEventKind.Move }] };
        var error = await Assert.ThrowsAsync<IOException>(() => new MacroPlayback(sink).RunAsync(macro, CancellationToken.None));
        Assert.Equal("injection failed", error.Message);
        Assert.Equal(MacroEventKind.KeyUp, sink.Events.Last().Kind);
    }

    [Fact]
    public async Task Balanced_input_does_not_generate_extra_releases()
    {
        var sink = new RecordingSink();
        var macro = new RecordedMacro { Events =
        [new MacroEvent { Kind = MacroEventKind.XDown, Code = 2 }, new MacroEvent { Kind = MacroEventKind.XUp, Code = 2 }] };
        await new MacroPlayback(sink).RunAsync(macro, CancellationToken.None);
        Assert.Equal(2, sink.Events.Count);
    }

    [Fact]
    public async Task Invalid_file_is_rejected_before_any_input_is_sent()
    {
        var sink = new RecordingSink();
        var macro = new RecordedMacro { Events =
        [new MacroEvent { OffsetTicks = 100 }, new MacroEvent { OffsetTicks = 1 }] };
        await Assert.ThrowsAsync<InvalidDataException>(() => new MacroPlayback(sink).RunAsync(macro, CancellationToken.None));
        Assert.Empty(sink.Events);
    }

    [Fact]
    public async Task Save_round_trip_preserves_events_and_does_not_overwrite_previous_recordings()
    {
        var root = Path.Combine(Path.GetTempPath(), "GestureClipMacroTests", Guid.NewGuid().ToString("N"));
        var store = new MacroFileStore(new AppPathProvider(root));
        var macro = new RecordedMacro { Name = "测试宏", Events =
        [new MacroEvent { Kind = MacroEventKind.Move, X = -400, Y = 500, OffsetTicks = 123 }] };
        var first = await store.SaveAsync(macro);
        var second = await store.SaveAsync(macro);
        Assert.NotEqual(first, second);
        var loaded = await store.LoadAsync(first);
        Assert.Equal(macro.Name, loaded.Name);
        Assert.Equal(macro.Events, loaded.Events);
        Assert.Equal(2, store.ListFiles().Count);
        Assert.Empty(Directory.GetFiles(store.DirectoryPath, "*.tmp"));
    }

    [Fact]
    public async Task Progress_counts_successful_events_and_excludes_cleanup_and_cancelled_events()
    {
        using var cancellation = new CancellationTokenSource();
        var progress = new MacroPlaybackProgress();
        var sink = new RecordingSink { OnSend = input =>
        {
            if (input.Kind == MacroEventKind.KeyDown) cancellation.Cancel();
        } };
        var macro = new RecordedMacro { Events =
        [new MacroEvent { Kind = MacroEventKind.KeyDown, Code = 65 }, new MacroEvent { Kind = MacroEventKind.KeyUp, Code = 65 }] };
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new MacroPlayback(sink).RunAsync(macro, cancellation.Token, progress));
        Assert.Equal(1, progress.CompletedEvents);
        Assert.Equal(2, sink.Events.Count); // The final release is cleanup, not a replayed event.
    }

    [Fact]
    public async Task Playback_and_cleanup_failures_are_both_preserved()
    {
        var progress = new MacroPlaybackProgress();
        var sink = new RecordingSink { OnSend = input =>
        {
            if (input.Kind == MacroEventKind.Move) throw new IOException("original injection failure");
            if (input.Kind == MacroEventKind.KeyUp) throw new InvalidOperationException("release failure");
        } };
        var macro = new RecordedMacro { Events =
        [new MacroEvent { Kind = MacroEventKind.KeyDown, Code = 65 }, new MacroEvent { Kind = MacroEventKind.Move }] };
        var error = await Assert.ThrowsAsync<AggregateException>(() => new MacroPlayback(sink).RunAsync(macro, CancellationToken.None, progress));
        Assert.Contains(error.Flatten().InnerExceptions, item => item.Message == "original injection failure");
        Assert.Contains(error.Flatten().InnerExceptions, item => item.Message == "release failure");
        Assert.Equal(1, progress.CompletedEvents);
    }

    private sealed class RecordingSink : IMacroInputSink
    {
        public List<MacroEvent> Events { get; } = [];
        public Action<MacroEvent>? OnSend { get; init; }
        public void Send(MacroEvent input) { OnSend?.Invoke(input); Events.Add(input); }
    }
}
