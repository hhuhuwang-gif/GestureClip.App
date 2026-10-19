namespace GestureClip.App.Services;

public enum ClipboardShortcutAction
{
    None,
    ToggleHelp,
    CloseOrClear,
    FocusSearch,
    SelectFilter,
    NextFilter,
    PreviousFilter,
    ScrollTop,
    ScrollBottom,
    SelectAll,
    UndoDelete,
    Copy,
    CopyPlain,
    CopyFiles,
    Pin,
    Favorite,
    Queue,
    Edit,
    Delete,
    Paste,
    PasteKeepOpen,
    PastePlain,
    PastePlainKeepOpen,
    PasteByIndex
}

public readonly record struct ClipboardShortcut(ClipboardShortcutAction Action, int Index = -1);
