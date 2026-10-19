using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using GestureClip.App.ViewModels;
using GestureClip.Core.Abstractions;
using GestureClip.Core.Clipboard;
using GestureClip.Core.Settings;

namespace GestureClip.App;

public partial class ClipboardOverlayWindow
{

    private async void QuickCopyItemButton_Click(object sender, RoutedEventArgs e)
    {
        if (GetItemFromSender(sender) is not { } item)
        {
            return;
        }

        SelectSingleItem(item);
        await _viewModel.CopySelectedAsync([item]);
        e.Handled = true;
    }

    private async void QuickPasteItemButton_Click(object sender, RoutedEventArgs e)
    {
        if (GetItemFromSender(sender) is not { } item)
        {
            return;
        }

        SelectSingleItem(item);
        await PasteWithFocusRestoreAsync(keepOverlayOpen: false);
        e.Handled = true;
    }

    private async Task CopyItemAndHideAsync(ClipboardItem item)
    {
        SelectSingleItem(item);
        if (await _viewModel.CopySelectedAsync([item]))
        {
            HideOverlayAndReleaseFocus();
        }
    }

    private async void QuickPinItemButton_Click(object sender, RoutedEventArgs e)
    {
        if (GetItemFromSender(sender) is not { } item)
        {
            return;
        }

        SelectSingleItem(item);
        await _viewModel.ToggleSelectedPinnedAsync();
        e.Handled = true;
    }

    private async void QuickDeleteItemButton_Click(object sender, RoutedEventArgs e)
    {
        if (GetItemFromSender(sender) is not { } item)
        {
            return;
        }

        SelectSingleItem(item);
        if (ConfirmDeleteSelectedItems([item]))
        {
            await _viewModel.DeleteItemsAsync([item]);
            SyncListSelectionFromViewModel();
        }

        e.Handled = true;
    }

    private bool ConfirmDeleteSelectedItems(IReadOnlyList<ClipboardItem> selectedItems)
    {
        if (selectedItems.Count == 0)
        {
            return false;
        }

        var message = selectedItems.Count == 1
            ? "这会从本机剪贴板历史里删除这条记录。删除后可立即 Ctrl+Z 撤销。不会影响当前系统剪贴板内容。是否继续？"
            : $"这会从本机剪贴板历史里删除选中的 {selectedItems.Count} 条记录。删除后可立即 Ctrl+Z 撤销。不会影响当前系统剪贴板内容。是否继续？";
        return _confirmationService.Confirm("删除剪贴板记录", message);
    }

    private IReadOnlyList<ClipboardItem> GetSelectedItems()
    {
        return HistoryList.SelectedItems.Cast<ClipboardItem>().ToArray();
    }

    private void SelectSingleItem(ClipboardItem item)
    {
        HistoryList.SelectedItems.Clear();
        HistoryList.SelectedItem = item;
        _viewModel.SelectedItem = item;
    }

    private static ClipboardItem? GetItemFromSender(object sender)
    {
        return sender is FrameworkElement { DataContext: ClipboardItem item }
            ? item
            : null;
    }

    private static ClipboardItem? GetItemFromOriginalSource(object source)
    {
        if (source is not DependencyObject dependencyObject)
        {
            return null;
        }

        return FindAncestor<ListBoxItem>(dependencyObject) is { DataContext: ClipboardItem item }
            ? item
            : null;
    }

    private static T? FindAncestor<T>(DependencyObject? current)
        where T : DependencyObject
    {
        while (current is not null)
        {
            if (current is T target)
            {
                return target;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }
}
