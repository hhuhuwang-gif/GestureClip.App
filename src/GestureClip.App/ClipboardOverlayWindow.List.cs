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

    private void SearchBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        // Up/Down while typing moves the list selection without leaving the search box.
        if (e.Key is not (Key.Down or Key.Up))
        {
            return;
        }

        MoveListSelection(e.Key == Key.Down ? 1 : -1);
        e.Handled = true;
    }

    private void MoveListSelection(int delta)
    {
        if (_viewModel.Items.Count == 0)
        {
            return;
        }

        var currentIndex = _viewModel.SelectedItem is { } current ? _viewModel.Items.IndexOf(current) : -1;
        var nextIndex = Math.Clamp(currentIndex + delta, 0, _viewModel.Items.Count - 1);
        var item = _viewModel.Items[nextIndex];
        HistoryList.SelectedItems.Clear();
        HistoryList.SelectedItem = item;
        _viewModel.SelectedItem = item;
        HistoryList.ScrollIntoView(item);
    }

    private void ScrollTopButton_Click(object sender, RoutedEventArgs e)
    {
        ScrollToTopAndSelectLatest();
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
            "Links" => ClipboardOverlayFilter.Links,
            _ => ClipboardOverlayFilter.All
        };
    }
}
