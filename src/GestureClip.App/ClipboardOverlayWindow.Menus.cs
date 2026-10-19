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

    private async void CopySelectedMenuItem_Click(object sender, RoutedEventArgs e)
    {
        await _viewModel.CopySelectedAsync(GetSelectedItems());
    }

    private async void CopyPlainTextMenuItem_Click(object sender, RoutedEventArgs e)
    {
        await _viewModel.CopySelectedAsPlainTextAsync(GetSelectedItems());
    }

    private async void CopyAsFilesMenuItem_Click(object sender, RoutedEventArgs e)
    {
        await _viewModel.CopySelectedAsFilesAsync(GetSelectedItems());
    }

    private async void EditItemMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (await _viewModel.OpenItemEditorAsync(GetSelectedItems().FirstOrDefault()))
        {
            ItemEditorTextBox.Focus();
            ItemEditorTextBox.CaretIndex = ItemEditorTextBox.Text.Length;
        }
    }

    private void ItemEditorCancelButton_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.CloseItemEditor();
    }

    private async void ItemEditorSaveButton_Click(object sender, RoutedEventArgs e)
    {
        await _viewModel.SaveItemEditAsync();
    }

    private async void ShowQrMenuItem_Click(object sender, RoutedEventArgs e)
    {
        await _viewModel.ShowQrForItemAsync(GetSelectedItems().FirstOrDefault());
    }

    private void QrCloseButton_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.CloseQr();
    }

    private async void PinImageMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var full = await _viewModel.PreparePinImageAsync(GetSelectedItems().FirstOrDefault());
        if (full?.TextContent is null)
        {
            return;
        }

        try
        {
            var image = GestureClip.Infrastructure.Clipboard.ClipboardImageFactory.CreateFrozenBitmapImage(full.TextContent);
            new PinImageWindow(image).Show();
        }
        catch (Exception ex)
        {
            _viewModel.SetStatusText($"贴图失败:{ex.Message}");
        }
    }

    private async void PasteSelectedMenuItem_Click(object sender, RoutedEventArgs e)
    {
        await PasteWithFocusRestoreAsync(keepOverlayOpen: false);
    }

    private async void PasteKeepOpenMenuItem_Click(object sender, RoutedEventArgs e)
    {
        await PasteWithFocusRestoreAsync(keepOverlayOpen: true);
    }

    private async void TogglePinnedMenuItem_Click(object sender, RoutedEventArgs e)
    {
        await _viewModel.ToggleSelectedPinnedAsync();
    }

    private void EnqueuePasteQueueMenuItem_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.EnqueueToPasteQueue(GetSelectedItems());
    }

    private void ClearPasteQueueButton_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.ClearPasteQueue();
    }

    private void AddSnippetButton_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.OpenSnippetEditor();
        Dispatcher.BeginInvoke(new Action(() => SnippetTextBox.Focus()), System.Windows.Threading.DispatcherPriority.Input);
    }

    private void SnippetCancelButton_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.CloseSnippetEditor();
    }

    private async void SnippetSaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (!await _viewModel.SaveSnippetAsync())
        {
            return;
        }

        if (FavoritesFilterButton.IsChecked == true)
        {
            await _viewModel.SearchAsync();
        }
        else
        {
            FavoritesFilterButton.IsChecked = true;
        }
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

    private async void TransformUpperMenuItem_Click(object sender, RoutedEventArgs e)
    {
        await _viewModel.TransformSelectedTextAsync(ClipboardTextTransform.UpperCase);
    }

    private async void TransformLowerMenuItem_Click(object sender, RoutedEventArgs e)
    {
        await _viewModel.TransformSelectedTextAsync(ClipboardTextTransform.LowerCase);
    }

    private async void TransformTrimMenuItem_Click(object sender, RoutedEventArgs e)
    {
        await _viewModel.TransformSelectedTextAsync(ClipboardTextTransform.CompactWhitespace);
    }

    private async void TransformJsonMenuItem_Click(object sender, RoutedEventArgs e)
    {
        await _viewModel.TransformSelectedTextAsync(ClipboardTextTransform.FormatJson);
    }

    private async void TransformJsonMinifyMenuItem_Click(object sender, RoutedEventArgs e)
    {
        await _viewModel.TransformSelectedTextAsync(ClipboardTextTransform.MinifyJson);
    }

    private async void TransformUrlDecodeMenuItem_Click(object sender, RoutedEventArgs e)
    {
        await _viewModel.TransformSelectedTextAsync(ClipboardTextTransform.UrlDecode);
    }
}
