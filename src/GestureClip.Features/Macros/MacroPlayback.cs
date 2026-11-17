using System.Diagnostics;
using GestureClip.Core.Abstractions;
using GestureClip.Core.Macros;

namespace GestureClip.Features.Macros;

public sealed class MacroPlayback(IMacroInputSink sink)
{
    public Task RunAsync(RecordedMacro macro, CancellationToken cancellationToken, MacroPlaybackProgress? progress = null) =>
        Task.Run(() => RunCoreAsync(macro, cancellationToken, progress), cancellationToken);

    private async Task RunCoreAsync(RecordedMacro macro, CancellationToken cancellationToken, MacroPlaybackProgress? progress)
    {
        macro.Validate();
        var held = new Dictionary<(MacroEventKind, uint), MacroEvent>();
        var watch = Stopwatch.StartNew();
        Exception? playbackFailure = null;
        try
        {
            foreach (var input in macro.Events)
            {
                var delay = TimeSpan.FromTicks(input.OffsetTicks) - watch.Elapsed;
                if (delay > TimeSpan.Zero) await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                sink.Send(input);
                TrackHeld(held, input);
                progress?.Advance();
            }
        }
        catch (Exception error) { playbackFailure = error; throw; }
        finally
        {
            try { ReleaseHeld(held.Values); }
            catch (Exception cleanupFailure) when (playbackFailure is not null)
            {
                throw new AggregateException("回放失败，且释放按键时也发生错误。", playbackFailure, cleanupFailure);
            }
        }
    }

    private static void TrackHeld(Dictionary<(MacroEventKind, uint), MacroEvent> held, MacroEvent input)
    {
        if (input.Release() is { } release)
        {
            held[(release.Kind, release.Code)] = release;
            return;
        }
        held.Remove((input.Kind, input.Code));
    }

    private void ReleaseHeld(IEnumerable<MacroEvent> releases)
    {
        List<Exception> failures = [];
        foreach (var release in releases)
        {
            try { sink.Send(release); }
            catch (Exception error) { failures.Add(error); }
        }
        if (failures.Count > 0) throw new AggregateException("回放结束后释放按键失败。", failures);
    }
}
