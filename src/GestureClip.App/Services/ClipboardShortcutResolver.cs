using System.Collections.Frozen;
using System.Windows.Input;

namespace GestureClip.App.Services;

public static class ClipboardShortcutResolver
{
    private const int LastQuickPasteIndex = 9;
    private const int FilterCount = 6;
    private static readonly FrozenDictionary<Key, ClipboardShortcutAction> ControlActions =
        new Dictionary<Key, ClipboardShortcutAction>
        {
            [Key.F] = ClipboardShortcutAction.FocusSearch,
            [Key.C] = ClipboardShortcutAction.Copy,
            [Key.P] = ClipboardShortcutAction.Pin,
            [Key.S] = ClipboardShortcutAction.Favorite,
            [Key.Q] = ClipboardShortcutAction.Queue,
            [Key.E] = ClipboardShortcutAction.Edit,
            [Key.Tab] = ClipboardShortcutAction.NextFilter
        }.ToFrozenDictionary();

    public static ClipboardShortcut Resolve(Key key, ModifierKeys modifiers, bool isSearchTyping)
    {
        if (key == Key.Enter)
        {
            return ResolvePaste(modifiers);
        }
        if (modifiers != ModifierKeys.None)
        {
            return ResolveModified(key, modifiers, isSearchTyping);
        }
        return ResolveUnmodified(key, isSearchTyping);
    }

    private static ClipboardShortcut ResolvePaste(ModifierKeys modifiers)
    {
        var action = modifiers switch
        {
            ModifierKeys.None => ClipboardShortcutAction.Paste,
            ModifierKeys.Control => ClipboardShortcutAction.PasteKeepOpen,
            ModifierKeys.Shift => ClipboardShortcutAction.PastePlain,
            ModifierKeys.Control | ModifierKeys.Shift => ClipboardShortcutAction.PastePlainKeepOpen,
            _ => ClipboardShortcutAction.None
        };
        return new ClipboardShortcut(action);
    }

    private static ClipboardShortcut ResolveModified(Key key, ModifierKeys modifiers, bool isSearchTyping)
    {
        if (modifiers == ModifierKeys.Alt && GetDigitIndex(key) is { } pasteIndex)
        {
            return new ClipboardShortcut(ClipboardShortcutAction.PasteByIndex, pasteIndex);
        }
        if (modifiers == ModifierKeys.Control)
        {
            return ResolveControl(key, isSearchTyping);
        }
        if (modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
        {
            return new ClipboardShortcut(key switch
            {
                Key.C => ClipboardShortcutAction.CopyPlain,
                Key.F => ClipboardShortcutAction.CopyFiles,
                Key.Tab => ClipboardShortcutAction.PreviousFilter,
                _ => ClipboardShortcutAction.None
            });
        }
        if (!isSearchTyping && modifiers == ModifierKeys.Shift && key is Key.Oem2 or Key.OemQuestion)
        {
            return new ClipboardShortcut(ClipboardShortcutAction.ToggleHelp);
        }
        return default;
    }

    private static ClipboardShortcut ResolveControl(Key key, bool isSearchTyping)
    {
        if (GetDigitIndex(key) is { } filterIndex && filterIndex < FilterCount)
        {
            return new ClipboardShortcut(ClipboardShortcutAction.SelectFilter, filterIndex);
        }
        if (ControlActions.TryGetValue(key, out var action))
        {
            return new ClipboardShortcut(action);
        }
        if (isSearchTyping)
        {
            return default;
        }
        return new ClipboardShortcut(key switch
        {
            Key.A => ClipboardShortcutAction.SelectAll,
            Key.Z => ClipboardShortcutAction.UndoDelete,
            Key.Home => ClipboardShortcutAction.ScrollTop,
            Key.End => ClipboardShortcutAction.ScrollBottom,
            _ => ClipboardShortcutAction.None
        });
    }

    private static ClipboardShortcut ResolveUnmodified(Key key, bool isSearchTyping)
    {
        if (key == Key.F1)
        {
            return new ClipboardShortcut(ClipboardShortcutAction.ToggleHelp);
        }
        if (key == Key.Escape)
        {
            return new ClipboardShortcut(ClipboardShortcutAction.CloseOrClear);
        }
        if (isSearchTyping)
        {
            return default;
        }
        if (GetDigitIndex(key) is { } pasteIndex)
        {
            return new ClipboardShortcut(ClipboardShortcutAction.PasteByIndex, pasteIndex);
        }
        return new ClipboardShortcut(key switch
        {
            Key.Home => ClipboardShortcutAction.ScrollTop,
            Key.End => ClipboardShortcutAction.ScrollBottom,
            Key.F2 => ClipboardShortcutAction.Edit,
            Key.Delete => ClipboardShortcutAction.Delete,
            _ => ClipboardShortcutAction.None
        });
    }

    private static int? GetDigitIndex(Key key)
    {
        if (key is Key.D0 or Key.NumPad0)
        {
            return LastQuickPasteIndex;
        }
        if (key >= Key.D1 && key <= Key.D9)
        {
            return key - Key.D1;
        }
        return key >= Key.NumPad1 && key <= Key.NumPad9 ? key - Key.NumPad1 : null;
    }
}
