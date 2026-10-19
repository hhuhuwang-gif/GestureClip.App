using System.Windows.Input;
using GestureClip.App.Services;

namespace GestureClip.App;

public partial class ClipboardOverlayWindow
{
    private async Task<bool> TryExecutePasteShortcutAsync(ClipboardShortcut shortcut)
    {
        switch (shortcut.Action)
        {
            case ClipboardShortcutAction.Paste:
                await PasteWithFocusRestoreAsync(false);
                break;
            case ClipboardShortcutAction.PasteKeepOpen:
                await PasteWithFocusRestoreAsync(true);
                break;
            case ClipboardShortcutAction.PastePlain:
                await PasteWithFocusRestoreAsync(false, true);
                break;
            case ClipboardShortcutAction.PastePlainKeepOpen:
                await PasteWithFocusRestoreAsync(true, true);
                break;
            case ClipboardShortcutAction.PasteByIndex:
                await PasteByIndexWithFocusRestoreAsync(shortcut.Index);
                break;
            default: return false;
        }
        return true;
    }

    private async Task<bool> TryExecuteCopyShortcutAsync(ClipboardShortcutAction action)
    {
        if (action is not (ClipboardShortcutAction.Copy or ClipboardShortcutAction.CopyPlain or ClipboardShortcutAction.CopyFiles))
        {
            return false;
        }
        await _viewModel.FlushPendingSearchAsync();
        switch (action)
        {
            case ClipboardShortcutAction.Copy: await _viewModel.CopySelectedAsync(GetSelectedItems()); break;
            case ClipboardShortcutAction.CopyPlain: await _viewModel.CopySelectedAsPlainTextAsync(GetSelectedItems()); break;
            case ClipboardShortcutAction.CopyFiles: await _viewModel.CopySelectedAsFilesAsync(GetSelectedItems()); break;
        }
        return true;
    }

    private async Task<bool> TryExecuteRecordShortcutAsync(ClipboardShortcutAction action)
    {
        if (!IsRecordShortcut(action))
        {
            return false;
        }
        await _viewModel.FlushPendingSearchAsync();
        switch (action)
        {
            case ClipboardShortcutAction.Pin: await _viewModel.ToggleSelectedPinnedAsync(); break;
            case ClipboardShortcutAction.Favorite: await _viewModel.ToggleSelectedFavoriteAsync(); break;
            case ClipboardShortcutAction.Queue: _viewModel.EnqueueToPasteQueue(GetSelectedItems()); break;
            case ClipboardShortcutAction.Edit: await EditSelectedItemAsync(); break;
            case ClipboardShortcutAction.Delete: await DeleteSelectedItemsAsync(); break;
            case ClipboardShortcutAction.UndoDelete:
                await _viewModel.UndoLastDeleteAsync();
                SyncListSelectionFromViewModel();
                break;
        }
        return true;
    }

    private static bool IsRecordShortcut(ClipboardShortcutAction action) =>
        action is ClipboardShortcutAction.Pin or ClipboardShortcutAction.Favorite or ClipboardShortcutAction.Queue or
            ClipboardShortcutAction.Edit or ClipboardShortcutAction.Delete or ClipboardShortcutAction.UndoDelete;

    private async Task EditSelectedItemAsync()
    {
        if (await _viewModel.OpenItemEditorAsync(GetSelectedItems().FirstOrDefault()))
        {
            ItemEditorTextBox.Focus();
            ItemEditorTextBox.CaretIndex = ItemEditorTextBox.Text.Length;
        }
    }

    private async Task DeleteSelectedItemsAsync()
    {
        var selected = GetSelectedItems();
        if (selected.Count == 0 || !ConfirmDeleteSelectedItems(selected))
        {
            return;
        }
        await _viewModel.DeleteItemsAsync(selected);
        SyncListSelectionFromViewModel();
    }
}
