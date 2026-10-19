using System.Windows.Input;
using GestureClip.App.Services;
using Xunit;

namespace GestureClip.Tests.App;

public sealed class ClipboardShortcutResolverTests
{
    [Theory]
    [InlineData(ModifierKeys.None, ClipboardShortcutAction.Paste)]
    [InlineData(ModifierKeys.Control, ClipboardShortcutAction.PasteKeepOpen)]
    [InlineData(ModifierKeys.Shift, ClipboardShortcutAction.PastePlain)]
    [InlineData(ModifierKeys.Control | ModifierKeys.Shift, ClipboardShortcutAction.PastePlainKeepOpen)]
    [InlineData(ModifierKeys.Alt, ClipboardShortcutAction.None)]
    public void Enter_routes_paste_modes_without_hijacking_alt(ModifierKeys modifiers, ClipboardShortcutAction expected)
    {
        Assert.Equal(expected, ClipboardShortcutResolver.Resolve(Key.Enter, modifiers, true).Action);
    }

    [Theory]
    [InlineData(Key.D1)]
    [InlineData(Key.D0)]
    [InlineData(Key.NumPad5)]
    [InlineData(Key.Home)]
    [InlineData(Key.End)]
    [InlineData(Key.F2)]
    [InlineData(Key.Delete)]
    public void Search_keeps_text_editing_keys(Key key)
    {
        Assert.Equal(ClipboardShortcutAction.None, ClipboardShortcutResolver.Resolve(key, ModifierKeys.None, true).Action);
    }

    [Theory]
    [InlineData(Key.D1, 0)]
    [InlineData(Key.D0, 9)]
    [InlineData(Key.NumPad5, 4)]
    public void Alt_digit_pastes_by_index_from_search(Key key, int expectedIndex)
    {
        var shortcut = ClipboardShortcutResolver.Resolve(key, ModifierKeys.Alt, true);
        Assert.Equal(ClipboardShortcutAction.PasteByIndex, shortcut.Action);
        Assert.Equal(expectedIndex, shortcut.Index);
    }

    [Theory]
    [InlineData(Key.Tab, ModifierKeys.Control, ClipboardShortcutAction.NextFilter)]
    [InlineData(Key.Tab, ModifierKeys.Control | ModifierKeys.Shift, ClipboardShortcutAction.PreviousFilter)]
    [InlineData(Key.F, ModifierKeys.Control | ModifierKeys.Shift, ClipboardShortcutAction.CopyFiles)]
    [InlineData(Key.C, ModifierKeys.Control | ModifierKeys.Shift, ClipboardShortcutAction.CopyPlain)]
    [InlineData(Key.E, ModifierKeys.Control, ClipboardShortcutAction.Edit)]
    [InlineData(Key.F, ModifierKeys.Control, ClipboardShortcutAction.FocusSearch)]
    public void Office_shortcuts_work_while_search_has_focus(Key key, ModifierKeys modifiers, ClipboardShortcutAction expected)
    {
        Assert.Equal(expected, ClipboardShortcutResolver.Resolve(key, modifiers, true).Action);
    }

    [Theory]
    [InlineData(Key.D1, ModifierKeys.Control | ModifierKeys.Shift)]
    [InlineData(Key.C, ModifierKeys.Control | ModifierKeys.Alt)]
    [InlineData(Key.D7, ModifierKeys.Control)]
    [InlineData(Key.D1, ModifierKeys.Windows)]
    public void Extra_modifiers_never_turn_into_unintended_paste(Key key, ModifierKeys modifiers)
    {
        Assert.Equal(ClipboardShortcutAction.None, ClipboardShortcutResolver.Resolve(key, modifiers, false).Action);
    }

    [Theory]
    [InlineData(Key.A)]
    [InlineData(Key.Z)]
    public void Search_retains_native_select_all_and_undo(Key key)
    {
        Assert.Equal(ClipboardShortcutAction.None, ClipboardShortcutResolver.Resolve(key, ModifierKeys.Control, true).Action);
    }
}
