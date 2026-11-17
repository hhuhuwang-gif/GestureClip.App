using System.Windows;
using System.Windows.Automation;
using System.Windows.Input;
using Button = System.Windows.Controls.Button;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace GestureClip.App;

public partial class SettingsWindow
{
    private void OpenMacroRecorder_Click(object sender, RoutedEventArgs e) => NavigateToPage("macro");

    private void MacroRecorderPage_Loaded(object sender, RoutedEventArgs e)
        => _appLifecycleService.InitializeMacroRecorder(MacroRecorderPage);

    private void SettingsWindow_SearchShortcut(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.F || Keyboard.Modifiers != ModifierKeys.Control) return;
        SettingsSearchBox.Focus();
        SettingsSearchBox.SelectAll();
        RefreshSearchResults();
        e.Handled = true;
    }

    private bool FocusSearchResult(Key key)
    {
        if (key is not (Key.Down or Key.Up)) return false;
        var results = SearchResultsPanel.Children.OfType<Button>().ToArray();
        if (!SearchResultsPopup.IsOpen || results.Length == 0) return false;
        var target = key == Key.Down ? results[0] : results[^1];
        target.Focus();
        target.BringIntoView();
        return true;
    }

    private void SearchResult_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        SettingsWindow_SearchShortcut(sender, e);
        if (e.Handled) return;
        if (e.Key == Key.Escape)
        {
            CloseSearchResults();
            SettingsSearchBox.Focus();
            e.Handled = true;
            return;
        }
        if (e.Key is not (Key.Up or Key.Down) || sender is not Button current) return;
        var results = SearchResultsPanel.Children.OfType<Button>().ToArray();
        var index = Array.IndexOf(results, current);
        if (e.Key == Key.Up && index == 0)
        {
            SettingsSearchBox.Focus();
            SettingsSearchBox.CaretIndex = SettingsSearchBox.Text.Length;
        }
        else
        {
            var nextIndex = Math.Clamp(index + (e.Key == Key.Down ? 1 : -1), 0, results.Length - 1);
            results[nextIndex].Focus();
            results[nextIndex].BringIntoView();
        }
        e.Handled = true;
    }

    private void ConfigureSearchResult(Button button, SettingsSearchEntry entry)
    {
        AutomationProperties.SetName(button, $"{entry.Title}，{entry.Path}");
        button.PreviewKeyDown += SearchResult_PreviewKeyDown;
    }
}
