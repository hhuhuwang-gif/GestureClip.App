namespace GestureClip.Core.Clipboard;

public enum PasteQueueStatus { Empty, Ready, Pasting, Failed }

public sealed record PasteQueueSnapshot(int PendingCount, PasteQueueStatus Status, string? ErrorMessage);
