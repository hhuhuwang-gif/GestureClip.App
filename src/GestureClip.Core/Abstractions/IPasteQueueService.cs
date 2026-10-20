using GestureClip.Core.Clipboard;

namespace GestureClip.Core.Abstractions;

/// <summary>
/// Sequential paste queue: Ctrl+V sends the current item, removing it only on success.
/// </summary>
public interface IPasteQueueService
{
    int PendingCount { get; }
    PasteQueueSnapshot Snapshot { get; }

    event EventHandler? QueueChanged;

    /// <summary>Returns false when the Ctrl+V hotkey could not be claimed (queue not activated).</summary>
    bool Enqueue(IReadOnlyList<ClipboardItem> items);

    void Clear();
    bool Retry();
    bool Skip();
}
