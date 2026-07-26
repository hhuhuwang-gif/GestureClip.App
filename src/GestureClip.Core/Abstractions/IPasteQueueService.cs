using GestureClip.Core.Clipboard;

namespace GestureClip.Core.Abstractions;

/// <summary>
/// Sequential paste queue: while items are pending, Ctrl+V pops and pastes the next one.
/// </summary>
public interface IPasteQueueService
{
    int PendingCount { get; }

    event EventHandler? QueueChanged;

    /// <summary>Returns false when the Ctrl+V hotkey could not be claimed (queue not activated).</summary>
    bool Enqueue(IReadOnlyList<ClipboardItem> items);

    void Clear();
}
