using System.Windows;
using System.Windows.Input;
using GestureClip.App.Services;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace GestureClip.App;

public partial class ClipboardOverlayWindow
{
    private async void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (HandleEditorKey(e))
        {
            return;
        }
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        var shortcut = ClipboardShortcutResolver.Resolve(key, Keyboard.Modifiers, SearchBox.IsKeyboardFocusWithin);
        if (shortcut.Action == ClipboardShortcutAction.None || SearchOwnsCopy(shortcut.Action))
        {
            return;
        }
        e.Handled = true;
        await ExecuteShortcutAsync(shortcut);
    }

    private bool HandleEditorKey(KeyEventArgs e)
    {
        var editorOpen = _viewModel.IsSnippetEditorOpen || _viewModel.IsItemEditorOpen || _viewModel.IsQrOpen;
        if (!editorOpen || e.Key != Key.Escape)
        {
            return editorOpen;
        }
        if (_viewModel.IsSnippetEditorOpen)
        {
            _viewModel.CloseSnippetEditor();
        }
        else if (_viewModel.IsItemEditorOpen)
        {
            _viewModel.CloseItemEditor();
        }
        else
        {
            _viewModel.CloseQr();
        }
        e.Handled = true;
        return true;
    }

    private bool SearchOwnsCopy(ClipboardShortcutAction action)
    {
        return SearchBox.IsKeyboardFocusWithin && SearchBox.SelectionLength > 0 &&
            action is ClipboardShortcutAction.Copy or ClipboardShortcutAction.CopyPlain;
    }

    private async Task ExecuteShortcutAsync(ClipboardShortcut shortcut)
    {
        if (await TryExecutePasteShortcutAsync(shortcut))
        {
            return;
        }
        if (await TryExecuteCopyShortcutAsync(shortcut.Action))
        {
            return;
        }
        if (await TryExecuteRecordShortcutAsync(shortcut.Action))
        {
            return;
        }
        await ExecuteNavigationShortcutAsync(shortcut);
    }

    private Task ExecuteNavigationShortcutAsync(ClipboardShortcut shortcut)
    {
        if (TryExecuteFilterShortcut(shortcut))
        {
            return Task.CompletedTask;
        }
        switch (shortcut.Action)
        {
            case ClipboardShortcutAction.FocusSearch: FocusSearchBox(); break;
            case ClipboardShortcutAction.SelectAll: HistoryList.SelectAll(); break;
            case ClipboardShortcutAction.ScrollTop: ScrollToTopAndSelectLatest(); break;
            case ClipboardShortcutAction.ScrollBottom: ScrollToBottom(); break;
            case ClipboardShortcutAction.ToggleHelp: _viewModel.ToggleShortcutHelp(); break;
            case ClipboardShortcutAction.CloseOrClear: return CloseOrClearAsync();
        }
        return Task.CompletedTask;
    }

    private async Task CloseOrClearAsync()
    {
        if (_viewModel.IsShortcutHelpVisible)
        {
            _viewModel.HideShortcutHelp();
            return;
        }
        if (await _viewModel.ClearSearchAsync())
        {
            FocusSearchBox();
            ScrollToTopAndSelectLatest();
            return;
        }
        Hide();
    }
}
