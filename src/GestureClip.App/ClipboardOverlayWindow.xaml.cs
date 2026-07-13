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

    public async Task LoadHistoryAsync()
    {
        await _viewModel.LoadAsync();
        ScrollToTopAndSelectLatest();
    }

    public void FocusSearchBox()
    {
        SearchBox.Focus();
        SearchBox.SelectAll();
    }

    private async void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (IsShortcutHelpToggleKey(e))
        {
            _viewModel.ToggleShortcutHelp();
            e.Handled = true;
            return;
        }

        if (_viewModel.IsShortcutHelpVisible && e.Key == Key.Escape)
        {
            _viewModel.HideShortcutHelp();
            e.Handled = true;
            return;
        }

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

        if (e.Key == Key.Home)
        {
            ScrollToTopAndSelectLatest();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.End)
        {
            ScrollToBottom();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Z &&
            (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control &&
            (Keyboard.Modifiers & ModifierKeys.Shift) == 0 &&
            _viewModel.CanUndoDelete &&
            !IsSearchBoxTyping())
        {
            await _viewModel.UndoLastDeleteAsync();
            SyncListSelectionFromViewModel();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Escape)
        {
            if (_viewModel.IsShortcutHelpVisible)
            {
                _viewModel.HideShortcutHelp();
                e.Handled = true;
                return;
            }

            if (await _viewModel.ClearSearchAsync())
            {
                FocusSearchBox();
                ScrollToTopAndSelectLatest();
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

        if (e.Key == Key.C &&
            (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control &&
            (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
        {
            await _viewModel.CopySelectedAsPlainTextAsync(GetSelectedItems());
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
            var selected = GetSelectedItems();
            if (selected.Count > 0 && ConfirmDeleteSelectedItems(selected))
            {
                await _viewModel.DeleteItemsAsync(selected);
                SyncListSelectionFromViewModel();
            }

            e.Handled = true;
            return;
        }

        if (e.Key == Key.Enter &&
            (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        {
            await _viewModel.PasteSelectedAsync(keepOverlayOpen: true);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Enter)
        {
            if (await _viewModel.PasteSelectedAsync(keepOverlayOpen: false))
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

    private bool IsSearchBoxTyping()
    {
        return SearchBox.IsKeyboardFocusWithin && !string.IsNullOrEmpty(SearchBox.Text);
    }

    private static bool IsShortcutHelpToggleKey(System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.F1)
        {
            return true;
        }

        // US/many layouts: Shift + /  → ?
        if ((e.Key is Key.Oem2 or Key.OemQuestion) &&
            (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
        {
            return true;
        }

        return false;
    }

    private void Window_Deactivated(object sender, EventArgs e)
    {
        if (_alwaysVisible || _isContextMenuOpen || _viewModel.IsShortcutHelpVisible)
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
            AlwaysVisibleButton.Content = enabled ? "📍" : "📌";
            AlwaysVisibleButton.ToolTip = enabled ? "常显中：点击关闭（点外部不关）" : "点击开启常显";
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

    private void ScrollTopButton_Click(object sender, RoutedEventArgs e)
    {
        ScrollToTopAndSelectLatest();
    }

    private void ShortcutHelpButton_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.ToggleShortcutHelp();
    }

    private void ShortcutHelpBackdrop_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _viewModel.HideShortcutHelp();
        e.Handled = true;
    }

    private void ShortcutHelpCard_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // Prevent backdrop close when clicking the card itself.
        e.Handled = true;
    }

    private void ScrollToTopAndSelectLatest()
    {
        _viewModel.SelectLatestItem();
        SyncListSelectionFromViewModel();
        if (FindScrollViewer(HistoryList) is { } scrollViewer)
        {
            scrollViewer.ScrollToVerticalOffset(0);
        }

        if (_viewModel.SelectedItem is { } item)
        {
            HistoryList.ScrollIntoView(item);
        }
    }

    private void ScrollToBottom()
    {
        if (FindScrollViewer(HistoryList) is { } scrollViewer)
        {
            scrollViewer.ScrollToEnd();
        }

        if (_viewModel.Items.Count > 0)
        {
            var last = _viewModel.Items[^1];
            HistoryList.SelectedItem = last;
            _viewModel.SelectedItem = last;
            HistoryList.ScrollIntoView(last);
        }
    }

    private void SyncListSelectionFromViewModel()
    {
        if (_viewModel.SelectedItem is null)
        {
            HistoryList.SelectedItems.Clear();
            return;
        }

        HistoryList.SelectedItems.Clear();
        HistoryList.SelectedItem = _viewModel.SelectedItem;
        HistoryList.ScrollIntoView(_viewModel.SelectedItem);
    }

    private static ScrollViewer? FindScrollViewer(DependencyObject root)
    {
        if (root is ScrollViewer scrollViewer)
        {
            return scrollViewer;
        }

        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            var found = FindScrollViewer(child);
            if (found is not null)
            {
                return found;
            }
        }

        return null;
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
        if (HistoryList.SelectedItem is ClipboardItem selected)
        {
            _viewModel.SelectedItem = selected;
        }
    }

    private async void ClearSearchButton_Click(object sender, RoutedEventArgs e)
    {
        await _viewModel.ResetViewAsync();
        if (AllFilterButton is not null)
        {
            AllFilterButton.IsChecked = true;
        }

        FocusSearchBox();
        ScrollToTopAndSelectLatest();
    }

    private async void LoadMoreButton_Click(object sender, RoutedEventArgs e)
    {
        await _viewModel.LoadMoreAsync();
    }

    private async void HistoryList_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (ScrollTopButton is not null)
        {
            ScrollTopButton.Opacity = e.VerticalOffset > 24 ? 1.0 : 0.55;
        }

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

    private async void CopyPlainTextMenuItem_Click(object sender, RoutedEventArgs e)
    {
        await _viewModel.CopySelectedAsPlainTextAsync(GetSelectedItems());
    }

    private async void PasteSelectedMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (await _viewModel.PasteSelectedAsync(keepOverlayOpen: false))
        {
            HideOverlayAndReleaseFocus();
        }
    }

    private async void PasteKeepOpenMenuItem_Click(object sender, RoutedEventArgs e)
    {
        await _viewModel.PasteSelectedAsync(keepOverlayOpen: true);
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
        SyncListSelectionFromViewModel();
    }

    private async void UndoDeleteMenuItem_Click(object sender, RoutedEventArgs e)
    {
        await _viewModel.UndoLastDeleteAsync();
        SyncListSelectionFromViewModel();
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
        if (await _viewModel.PasteSelectedAsync(keepOverlayOpen: false))
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

    private void HideOverlayAndReleaseFocus()
    {
        _viewModel.HideShortcutHelp();
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
