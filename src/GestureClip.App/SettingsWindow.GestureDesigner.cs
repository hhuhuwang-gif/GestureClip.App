using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;
using GestureClip.App.Controls;
using GestureClip.App.Services;
using GestureClip.App.ViewModels;
using GestureClip.Core.Gestures;
using WpfButton = System.Windows.Controls.Button;
using WpfButtonBase = System.Windows.Controls.Primitives.ButtonBase;
using WpfHorizontalAlignment = System.Windows.HorizontalAlignment;
using WpfKey = System.Windows.Input.Key;
using WpfKeyEventArgs = System.Windows.Input.KeyEventArgs;
using WpfRadioButton = System.Windows.Controls.RadioButton;
using WpfScrollBar = System.Windows.Controls.Primitives.ScrollBar;
using WpfSelector = System.Windows.Controls.Primitives.Selector;
using WpfTextBoxBase = System.Windows.Controls.Primitives.TextBoxBase;
using WpfThickness = System.Windows.Thickness;
using WpfVisibility = System.Windows.Visibility;
using WpfFontWeights = System.Windows.FontWeights;
using WpfBrush = System.Windows.Media.Brush;

namespace GestureClip.App;

public partial class SettingsWindow
{
    private void RecordGesturePad_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement element || DataContext is not SettingsViewModel)
        {
            return;
        }

        _isRecordingGesture = true;
        _recordedGesturePoints.Clear();
        RecordGesturePolyline.Points.Clear();
        element.CaptureMouse();
        AddRecordedGesturePoint(element, e.GetPosition(element));
        e.Handled = true;
    }

    private void RecordGesturePad_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (!_isRecordingGesture || sender is not FrameworkElement element)
        {
            return;
        }

        AddRecordedGesturePoint(element, e.GetPosition(element));
        e.Handled = true;
    }

    private void RecordGesturePad_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isRecordingGesture || sender is not FrameworkElement element)
        {
            return;
        }

        AddRecordedGesturePoint(element, e.GetPosition(element));
        _isRecordingGesture = false;
        element.ReleaseMouseCapture();
        if (DataContext is SettingsViewModel viewModel)
        {
            viewModel.SetNewGesturePatternFromRecordedPoints(_recordedGesturePoints);
        }

        e.Handled = true;
    }

    private void AddRecordedGesturePoint(FrameworkElement element, System.Windows.Point point)
    {
        var x = (int)Math.Round(Math.Clamp(point.X, 0, element.ActualWidth));
        var y = (int)Math.Round(Math.Clamp(point.Y, 0, element.ActualHeight));
        var now = DateTimeOffset.UtcNow;
        if (_recordedGesturePoints.Count > 0)
        {
            var previous = _recordedGesturePoints[^1];
            var dx = previous.X - x;
            var dy = previous.Y - y;
            if (Math.Sqrt(dx * dx + dy * dy) < 3)
            {
                return;
            }
        }

        _recordedGesturePoints.Add(new GesturePoint(x, y, now));
        RecordGesturePolyline.Points.Add(new System.Windows.Point(x, y));
    }

}
