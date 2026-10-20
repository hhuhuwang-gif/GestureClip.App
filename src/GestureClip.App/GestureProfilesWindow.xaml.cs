using System.Windows;
using System.Windows.Controls;
using Button = System.Windows.Controls.Button;
using GestureClip.App.ViewModels;

namespace GestureClip.App;

public partial class GestureProfilesWindow : Window
{
    private readonly GestureProfilesViewModel _viewModel;

    public GestureProfilesWindow(GestureProfilesViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
    }

    private void NewProfile_Click(object sender, RoutedEventArgs args) => _viewModel.NewProfile();

    private void AddBinding_Click(object sender, RoutedEventArgs args) => _viewModel.Bindings.Add(new());

    private void RemoveBinding_Click(object sender, RoutedEventArgs args)
    {
        if (BindingsGrid.SelectedItem is GestureProfileBindingRow row) _viewModel.Bindings.Remove(row);
    }

    private void ChooseProgram_Click(object sender, RoutedEventArgs args)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog { Filter = "应用程序 (*.exe)|*.exe", CheckFileExists = true };
        if (dialog.ShowDialog(this) != true) return;
        _viewModel.ProcessName = System.IO.Path.GetFileName(dialog.FileName);
    }

    private void Template_Click(object sender, RoutedEventArgs args)
    {
        try { _viewModel.ApplyTemplate((string)((Button)sender).Content); }
        catch (InvalidOperationException exception) { ShowError(exception); }
    }

    private async void SaveProfile_Click(object sender, RoutedEventArgs args)
    {
        BindingsGrid.CommitEdit(DataGridEditingUnit.Cell, true);
        BindingsGrid.CommitEdit(DataGridEditingUnit.Row, true);
        IsEnabled = false;
        try { await _viewModel.SaveAsync(); }
        catch (Exception exception) { ShowError(exception); }
        finally { IsEnabled = true; }
    }

    private async void DeleteProfile_Click(object sender, RoutedEventArgs args)
    {
        IsEnabled = false;
        try { await _viewModel.DeleteAsync(); }
        catch (Exception exception) { ShowError(exception); }
        finally { IsEnabled = true; }
    }

    private void ShowError(Exception exception) =>
        System.Windows.MessageBox.Show(this, exception.Message, "应用手势配置未保存", MessageBoxButton.OK, MessageBoxImage.Error);
}
