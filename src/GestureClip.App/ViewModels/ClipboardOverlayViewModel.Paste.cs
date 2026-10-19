using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using GestureClip.Core.Abstractions;
using GestureClip.Core.Clipboard;
using GestureClip.Features.Assistant;
using GestureClip.Features.Clipboard;

namespace GestureClip.App.ViewModels;


public sealed partial class ClipboardOverlayViewModel
{
    public async Task<bool> PasteSelectedAsync(bool keepOverlayOpen = false, bool plainText = false)
    {
        await FlushPendingSearchAsync();
        if (SelectedItem is null || HasSearchError)
        {
            return false;
        }

        var ok = await TryPasteAsync(SelectedItem, plainText);
        if (ok)
        {
            StatusText = keepOverlayOpen ? "已粘贴（面板保持打开）" : "已粘贴";
        }

        return ok;
    }

    public async Task<bool> PasteByIndexAsync(int index)
    {
        await FlushPendingSearchAsync();
        if (HasSearchError || index < 0 || index >= Items.Count)
        {
            return false;
        }

        var ok = await TryPasteAsync(Items[index]);
        if (ok)
        {
            StatusText = "已粘贴";
        }

        return ok;
    }

    private async Task<bool> TryPasteAsync(ClipboardItem item, bool plainText = false)
    {
        try
        {
            ErrorMessage = null;
            await _clipboardService.PasteAsync(item, new PasteOptions(plainText), CancellationToken.None);
            MarkItemsUsed([item.Id]);
            return true;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"粘贴剪贴板历史失败：{ex.Message}";
            StatusText = "粘贴失败";
            return false;
        }
    }

    public async Task<bool> CopySelectedAsync(IReadOnlyList<ClipboardItem> selectedItems)
    {
        if (selectedItems.Count == 0)
        {
            return false;
        }

        try
        {
            ErrorMessage = null;
            await _clipboardService.CopyItemsAsync(selectedItems, CancellationToken.None);
            MarkItemsUsed(selectedItems.Select(item => item.Id).ToArray());
            StatusText = selectedItems.Count == 1 && selectedItems[0].IsImage
                ? "图片已复制 · 色条变绿 · 面板未关闭"
                : selectedItems.Count == 1
                    ? "文字已复制 · 色条变绿 · 面板未关闭"
                    : $"已合并复制 {selectedItems.Count} 条 · 已复制项色条变绿";
            return true;
        }
        catch (NotSupportedException ex)
        {
            StatusText = ex.Message;
            return false;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"复制剪贴板历史失败：{ex.Message}";
            StatusText = selectedItems.Count == 1 && selectedItems[0].IsImage
                ? $"图片复制失败：{ex.Message}"
                : "复制失败";
            return false;
        }
    }

    public async Task<bool> DeleteItemsAsync(IReadOnlyList<ClipboardItem> selectedItems)
    {
        if (selectedItems.Count == 0)
        {
            return false;
        }

        // Prefer full content for undo (images / long text).
        var stash = new List<ClipboardItem>(selectedItems.Count);
        foreach (var item in selectedItems)
        {
            var full = await _clipboardService.GetByIdAsync(item.Id, CancellationToken.None) ?? item;
            stash.Add(full);
        }

        var ids = stash.Select(item => item.Id).ToArray();
        var deleted = await _clipboardService.DeleteItemsAsync(ids, CancellationToken.None);
        if (deleted <= 0)
        {
            StatusText = "删除失败";
            return false;
        }

        _undoDeleteItems = stash;
        OnPropertyChanged(nameof(CanUndoDelete));
        RefreshShortcutHint();
        RemoveItems(ids);
        StatusText = deleted == 1
            ? "已删除 1 条 · Ctrl+Z 撤销"
            : $"已删除 {deleted} 条 · Ctrl+Z 撤销";
        return true;
    }

    public async Task<bool> UndoLastDeleteAsync()
    {
        if (_undoDeleteItems.Count == 0)
        {
            return false;
        }

        var items = _undoDeleteItems;
        try
        {
            await _clipboardService.RestoreItemsAsync(items, CancellationToken.None);
            _undoDeleteItems = [];
            OnPropertyChanged(nameof(CanUndoDelete));
            RefreshShortcutHint();
            await SearchAsync();
            SelectedItem = Items.FirstOrDefault(item => item.Id == items[0].Id) ?? Items.FirstOrDefault();
            UpdateSelectedCount(SelectedItem is null ? 0 : 1);
            StatusText = items.Count == 1
                ? "已撤销删除"
                : $"已撤销删除 {items.Count} 条";
            return true;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"撤销删除失败：{ex.Message}";
            StatusText = "撤销失败";
            return false;
        }
    }

    private void RefreshShortcutHint()
    {
        ShortcutHintText = CanUndoDelete
            ? "Ctrl+Z 撤销删除 · Home 回顶 · Ctrl+Enter 粘贴不关 · ? 快捷键"
            : DefaultShortcutHint;
        OnPropertyChanged(nameof(ShortcutHintText));
    }

}
