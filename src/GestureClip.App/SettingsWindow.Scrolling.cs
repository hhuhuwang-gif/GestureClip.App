using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using GestureClip.App.Controls;

namespace GestureClip.App;

public partial class SettingsWindow
{
    private void SettingsWindow_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (e.Handled)
        {
            return;
        }

        // A closed ComboBox would otherwise swallow the wheel and silently change
        // its selected value while the page stays put. Forward the delta to the
        // page scroller instead so scrolling always feels continuous.
        var comboBox = FindAncestor<System.Windows.Controls.ComboBox>(e.OriginalSource as DependencyObject);
        if (comboBox is null || comboBox.IsDropDownOpen)
        {
            return;
        }

        e.Handled = true;

        var page = FindAncestor<ScrollViewer>(VisualTreeHelper.GetParent(comboBox));
        if (page is null)
        {
            return;
        }

        if (page is SmoothScrollViewer smooth)
        {
            smooth.TryScrollByWheel(e.Delta);
            return;
        }

        page.RaiseEvent(new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
        {
            RoutedEvent = Mouse.MouseWheelEvent,
            Source = page
        });
    }
}
