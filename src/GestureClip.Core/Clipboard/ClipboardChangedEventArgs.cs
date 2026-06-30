namespace GestureClip.Core.Clipboard;

public sealed class ClipboardChangedEventArgs : EventArgs
{
    public DateTimeOffset ChangedAt { get; init; } = DateTimeOffset.UtcNow;
}
