using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using GestureClip.App.ViewModels;
using Button = System.Windows.Controls.Button;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

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

    private void Window_Loaded(object sender, RoutedEventArgs args)
    {
        if (_viewModel.SelectedProfile is null) FocusProcessName();
    }

    private async void Window_PreviewKeyDown(object sender, KeyEventArgs args)
    {
        if (args.Key == Key.Escape)
        {
            Close();
            args.Handled = true;
            return;
        }
        if (Keyboard.Modifiers == ModifierKeys.Control && await HandleControlShortcutAsync(args.Key))
        {
            args.Handled = true;
            return;
        }
        if (args.Key == Key.Insert)
        {
            AddBinding();
            args.Handled = true;
        }
        else if (args.Key == Key.Delete && BindingsGrid.IsKeyboardFocusWithin)
        {
            RemoveSelectedBinding();
            args.Handled = true;
        }
    }

    private async Task<bool> HandleControlShortcutAsync(Key key)
    {
        switch (key)
        {
            case Key.N: CreateProfile(); return true;
            case Key.D: DuplicateProfile(); return true;
            case Key.O: ChooseProgram(); return true;
            case Key.S: await SaveProfileAsync(); return true;
            case Key.Delete: await DeleteProfileAsync(); return true;
            default: return false;
        }
    }

    private void NewProfile_Click(object sender, RoutedEventArgs args) => CreateProfile();

    private void DuplicateProfile_Click(object sender, RoutedEventArgs args) => DuplicateProfile();

    private void AddBinding_Click(object sender, RoutedEventArgs args) => AddBinding();

    private void RemoveBinding_Click(object sender, RoutedEventArgs args) => RemoveSelectedBinding();

    private void ChooseProgram_Click(object sender, RoutedEventArgs args) => ChooseProgram();

    private async void SaveProfile_Click(object sender, RoutedEventArgs args) => await SaveProfileAsync();

    private async void DeleteProfile_Click(object sender, RoutedEventArgs args) => await DeleteProfileAsync();

    private void CreateProfile()
    {
        _viewModel.NewProfile();
        FocusProcessName();
    }

    private void DuplicateProfile()
    {
        _viewModel.DuplicateSelectedProfile();
        FocusProcessName();
    }

    private void AddBinding()
    {
        var row = _viewModel.AddBinding();
        BindingsGrid.SelectedItem = row;
        BindingsGrid.ScrollIntoView(row);
        BindingsGrid.Focus();
    }

    private void RemoveSelectedBinding() =>
        _viewModel.RemoveBinding(BindingsGrid.SelectedItem as GestureProfileBindingRow);

    private void ChooseProgram()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "应用程序 (*.exe)|*.exe",
            CheckFileExists = true
        };
        if (dialog.ShowDialog(this) != true) return;
        _viewModel.ProcessName = System.IO.Path.GetFileName(dialog.FileName);
        FocusProcessName();
    }

    private void Template_Click(object sender, RoutedEventArgs args)
    {
        try
        {
            _viewModel.ApplyTemplate((string)((Button)sender).Content);
        }
        catch (InvalidOperationException exception)
        {
            ShowError(exception);
        }
    }

    private void AppendDirection_Click(object sender, RoutedEventArgs args)
    {
        if (sender is not Button { Tag: string direction }) return;
        var row = BindingsGrid.SelectedItem as GestureProfileBindingRow ?? _viewModel.AddBinding();
        if (BindingsGrid.SelectedItem is null) row.Pattern = string.Empty;
        row.Pattern += direction;
        BindingsGrid.SelectedItem = row;
        BindingsGrid.Items.Refresh();
    }

    private void RemoveLastDirection_Click(object sender, RoutedEventArgs args)
    {
        if (BindingsGrid.SelectedItem is not GestureProfileBindingRow row || row.Pattern.Length == 0) return;
        row.Pattern = row.Pattern[..^1];
        BindingsGrid.Items.Refresh();
    }

    private async Task SaveProfileAsync()
    {
        CommitGridEdits();
        IsEnabled = false;
        try
        {
            await _viewModel.SaveAsync();
        }
        catch (Exception exception)
        {
            ShowError(exception);
        }
        finally
        {
            IsEnabled = true;
        }
    }

    private async Task DeleteProfileAsync()
    {
        if (_viewModel.SelectedProfile is null) return;
        var answer = System.Windows.MessageBox.Show(this, "删除后，该应用将恢复使用全局手势。", "删除应用配置",
            MessageBoxButton.OKCancel, MessageBoxImage.Warning);
        if (answer != MessageBoxResult.OK) return;
        IsEnabled = false;
        try
        {
            await _viewModel.DeleteAsync();
        }
        catch (Exception exception)
        {
            ShowError(exception);
        }
        finally
        {
            IsEnabled = true;
        }
    }

    private void CommitGridEdits()
    {
        BindingsGrid.CommitEdit(DataGridEditingUnit.Cell, true);
        BindingsGrid.CommitEdit(DataGridEditingUnit.Row, true);
    }

    private void FocusProcessName()
    {
        ProcessNameInput.Focus();
        ProcessNameInput.SelectAll();
    }

    private void ShowError(Exception exception) =>
        System.Windows.MessageBox.Show(this, exception.Message, "应用手势配置未保存", MessageBoxButton.OK, MessageBoxImage.Error);
}
