using System.Windows;
using System.Windows.Input;
using GestureClip.App.ViewModels;

namespace GestureClip.App;

public partial class QuickActionCenterWindow : Window
{
    private readonly QuickActionCenterViewModel _viewModel;

    public QuickActionCenterWindow(QuickActionCenterViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;
    }

    public void PrepareForShow(string? hotkeyHint = null)
    {
        _viewModel.OnOpened(hotkeyHint);
        Dispatcher.BeginInvoke(() =>
        {
            SearchBox.Focus();
            SearchBox.SelectAll();
        });
    }

    private async void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Hide();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Down)
        {
            MoveSelectionAndReveal(1);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Up)
        {
            MoveSelectionAndReveal(-1);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.F5)
        {
            _viewModel.RefreshInputCommand.Execute(null);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Enter && (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
        {
            if (_viewModel.PreviewSelectedCommand.CanExecute(null))
            {
                _viewModel.PreviewSelectedCommand.Execute(null);
            }
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Enter && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        {
            if (_viewModel.RunAndPasteCommand.CanExecute(null))
            {
                _viewModel.RunAndPasteCommand.Execute(null);
            }

            e.Handled = true;
            return;
        }

        if (e.Key == Key.Enter)
        {
            await _viewModel.RunDefaultAsync();
            e.Handled = true;
        }
    }

    private void Window_Deactivated(object? sender, EventArgs e)
    {
        // Keep it light: hide when focus leaves, like clipboard overlay.
        if (IsVisible)
        {
            Hide();
        }
    }

    private async void ActionList_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        await _viewModel.RunDefaultAsync();
    }

    private void MoveSelectionAndReveal(int delta)
    {
        _viewModel.MoveSelection(delta);
        if (_viewModel.SelectedAction is not null)
        {
            ActionList.ScrollIntoView(_viewModel.SelectedAction);
        }
    }
}
