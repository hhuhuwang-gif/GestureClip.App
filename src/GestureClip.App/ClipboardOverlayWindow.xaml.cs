using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using GestureClip.App.ViewModels;
using GestureClip.Core.Abstractions;
using GestureClip.Core.Clipboard;
using GestureClip.Core.Settings;

namespace GestureClip.App;

public partial class ClipboardOverlayWindow : Window
{
    private readonly ClipboardOverlayViewModel _viewModel;
    private readonly ISettingsService _settingsService;
    private readonly IConfirmationService _confirmationService;
    private bool _alwaysVisible;
    private bool _isContextMenuOpen;

    public ClipboardOverlayWindow(
        ClipboardOverlayViewModel viewModel,
        ISettingsService settingsService,
        IConfirmationService confirmationService)
    {
        _viewModel = viewModel;
        _settingsService = settingsService;
        _confirmationService = confirmationService;
        InitializeComponent();
        DataContext = _viewModel;
        HistoryList.AlternationCount = 10;
        SetAlwaysVisible(_settingsService.Get(SettingKeys.ClipboardOverlayAlwaysVisible, false), persist: false);
    }

    public Task LoadHistoryAsync()
    {
        return _viewModel.LoadAsync();
    }

    public void FocusSearchBox()
    {
        SearchBox.Focus();
        SearchBox.SelectAll();
    }

    private async void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control &&
            SelectFilterByShortcut(e.Key))
        {
            e.Handled = true;
            return;
        }

        if (e.Key == Key.F && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        {
            FocusSearchBox();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Escape)
        {
            if (await _viewModel.ClearSearchAsync())
            {
                FocusSearchBox();
                e.Handled = true;
                return;
            }

            Hide();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.A && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        {
            HistoryList.SelectAll();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.C && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        {
            await _viewModel.CopySelectedAsync(GetSelectedItems());
            e.Handled = true;
            return;
        }

        if (e.Key == Key.P && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        {
            await _viewModel.ToggleSelectedPinnedAsync();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.S && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        {
            await _viewModel.ToggleSelectedFavoriteAsync();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Delete)
        {
            await _viewModel.DeleteItemsAsync(GetSelectedItems());
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Enter)
        {
            if (await _viewModel.PasteSelectedAsync())
            {
                HideOverlayAndReleaseFocus();
            }

            e.Handled = true;
            return;
        }

        var index = GetDigitIndex(e.Key);
        if (index is not null)
        {
            if (await _viewModel.PasteByIndexAsync(index.Value))
            {
                HideOverlayAndReleaseFocus();
            }

            e.Handled = true;
        }
    }

    private void Window_Deactivated(object sender, EventArgs e)
    {
        if (_alwaysVisible || _isContextMenuOpen)
        {
            return;
        }

        HideOverlayAndReleaseFocus();
    }

    private async void AlwaysVisibleButton_Click(object sender, RoutedEventArgs e)
    {
        SetAlwaysVisible(!_alwaysVisible, persist: true);
        await _settingsService.SetAsync(SettingKeys.ClipboardOverlayAlwaysVisible, _alwaysVisible, CancellationToken.None);
    }

    private void SetAlwaysVisible(bool enabled, bool persist)
    {
        _alwaysVisible = enabled;
        Topmost = enabled;
        if (AlwaysVisibleButton is not null)
        {
            AlwaysVisibleButton.Content = enabled ? "常显中" : "常显";
            AlwaysVisibleButton.ToolTip = enabled ? "点击关闭常显；当前点击外部不会关闭" : "点击开启常显；默认点击外部自动关闭";
            AlwaysVisibleButton.FontWeight = enabled ? FontWeights.SemiBold : FontWeights.Normal;
        }
    }

    private void ContextMenu_Opened(object sender, RoutedEventArgs e)
    {
        _isContextMenuOpen = true;
    }

    private void ContextMenu_Closed(object sender, RoutedEventArgs e)
    {
        _isContextMenuOpen = false;
    }

    private bool SelectFilterByShortcut(Key key)
    {
        var filterButton = key switch
        {
            Key.D1 or Key.NumPad1 => AllFilterButton,
            Key.D2 or Key.NumPad2 => PinnedFilterButton,
            Key.D3 or Key.NumPad3 => FavoritesFilterButton,
            Key.D4 or Key.NumPad4 => TextFilterButton,
            Key.D5 or Key.NumPad5 => ImagesFilterButton,
            _ => null
        };

        if (filterButton is null)
        {
            return false;
        }

        filterButton.IsChecked = true;
        return true;
    }

    private void HistoryList_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        var item = FindAncestor<ListBoxItem>((DependencyObject)e.OriginalSource);
        if (item is null)
        {
            return;
        }

        if (!item.IsSelected)
        {
            HistoryList.SelectedItems.Clear();
            item.IsSelected = true;
        }

        item.Focus();
    }

    private void HistoryList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _viewModel.UpdateSelectedCount(HistoryList.SelectedItems.Count);
    }

    private async void ClearSearchButton_Click(object sender, RoutedEventArgs e)
    {
        await _viewModel.ClearSearchAsync();
        FocusSearchBox();
    }

    private async void LoadMoreButton_Click(object sender, RoutedEventArgs e)
    {
        await _viewModel.LoadMoreAsync();
    }

    private async void HistoryList_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (e.VerticalChange <= 0 || !_viewModel.CanLoadMore)
        {
            return;
        }

        if (e.VerticalOffset + e.ViewportHeight < e.ExtentHeight - 48)
        {
            return;
        }

        await _viewModel.LoadMoreAsync();
    }

    private async void HistoryList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (GetItemFromOriginalSource(e.OriginalSource) is not { } item)
        {
            return;
        }

        await CopyItemAndHideAsync(item);
        e.Handled = true;
    }

    private void FilterRadioButton_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.RadioButton { Tag: string tag })
        {
            return;
        }

        _viewModel.SelectedFilter = tag switch
        {
            "Pinned" => ClipboardOverlayFilter.Pinned,
            "Favorites" => ClipboardOverlayFilter.Favorites,
            "Text" => ClipboardOverlayFilter.Text,
            "Images" => ClipboardOverlayFilter.Images,
            _ => ClipboardOverlayFilter.All
        };
    }

    private async void CopySelectedMenuItem_Click(object sender, RoutedEventArgs e)
    {
        await _viewModel.CopySelectedAsync(GetSelectedItems());
    }

    private async void PasteSelectedMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (await _viewModel.PasteSelectedAsync())
        {
            Hide();
        }
    }

    private async void TogglePinnedMenuItem_Click(object sender, RoutedEventArgs e)
    {
        await _viewModel.ToggleSelectedPinnedAsync();
    }

    private async void ToggleFavoriteMenuItem_Click(object sender, RoutedEventArgs e)
    {
        await _viewModel.ToggleSelectedFavoriteAsync();
    }

    private async void DeleteSelectedMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var selectedItems = GetSelectedItems();
        if (!ConfirmDeleteSelectedItems(selectedItems))
        {
            return;
        }

        await _viewModel.DeleteItemsAsync(selectedItems);
    }

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
        if (await _viewModel.PasteSelectedAsync())
        {
            HideOverlayAndReleaseFocus();
        }

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
            ? "这会从本机剪贴板历史里删除这条记录。删除后不会影响当前系统剪贴板内容。是否继续？"
            : $"这会从本机剪贴板历史里删除选中的 {selectedItems.Count} 条记录。删除后不会影响当前系统剪贴板内容。是否继续？";
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

    private void HideOverlayAndReleaseFocus()
    {
        MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
        Hide();
    }

    private static int? GetDigitIndex(Key key)
    {
        var keyValue = (int)key;

        if (key is Key.D0 or Key.NumPad0)
        {
            return 9;
        }

        if (keyValue >= (int)Key.D1 && keyValue <= (int)Key.D9)
        {
            return keyValue - (int)Key.D1;
        }

        if (keyValue >= (int)Key.NumPad1 && keyValue <= (int)Key.NumPad9)
        {
            return keyValue - (int)Key.NumPad1;
        }

        return null;
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
