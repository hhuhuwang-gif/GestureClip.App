using System.Windows;
using System.Windows.Input;
using GestureClip.App.ViewModels;

namespace GestureClip.App;

public partial class ClipboardOverlayWindow : Window
{
    private readonly ClipboardOverlayViewModel _viewModel;

    public ClipboardOverlayWindow(ClipboardOverlayViewModel viewModel)
    {
        _viewModel = viewModel;
        InitializeComponent();
        DataContext = _viewModel;
        HistoryList.AlternationCount = 10;
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
        if (e.Key == Key.Escape)
        {
            Hide();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Enter)
        {
            if (await _viewModel.PasteSelectedAsync())
            {
                Hide();
            }

            e.Handled = true;
            return;
        }

        var index = GetDigitIndex(e.Key);
        if (index is not null)
        {
            if (await _viewModel.PasteByIndexAsync(index.Value))
            {
                Hide();
            }

            e.Handled = true;
        }
    }

    private async void HistoryList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (await _viewModel.PasteSelectedAsync())
        {
            Hide();
        }
    }

    private static int? GetDigitIndex(Key key)
    {
        var keyValue = (int)key;

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
}
