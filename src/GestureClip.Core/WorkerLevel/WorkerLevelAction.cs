namespace GestureClip.Core.WorkerLevel;

public enum WorkerLevelAction
{
    None = 0,
    Copy,
    Paste,
    Cut,
    SelectAll,
    Undo,
    Redo,
    Enter,
    Escape,
    OpenClipboardOverlay,
    PasteLatestClipboardItem,
    Navigation,
    Other
}
