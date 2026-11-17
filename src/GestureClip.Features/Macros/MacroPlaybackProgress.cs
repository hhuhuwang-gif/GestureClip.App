namespace GestureClip.Features.Macros;

// Shared by the playback worker and UI timer; input events never enqueue UI work.
public sealed class MacroPlaybackProgress
{
    private int _completedEvents;
    public int CompletedEvents => Volatile.Read(ref _completedEvents);
    internal void Advance() => Interlocked.Increment(ref _completedEvents);
}
