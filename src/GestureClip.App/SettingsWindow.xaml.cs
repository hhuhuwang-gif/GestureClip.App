using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using GestureClip.Core.Gestures;
using GestureClip.App.Services;
using GestureClip.App.ViewModels;
using WpfButtonBase = System.Windows.Controls.Primitives.ButtonBase;
using WpfScrollBar = System.Windows.Controls.Primitives.ScrollBar;
using WpfSelector = System.Windows.Controls.Primitives.Selector;
using WpfTextBoxBase = System.Windows.Controls.Primitives.TextBoxBase;

namespace GestureClip.App;

public partial class SettingsWindow : Window
{
    private readonly AppLifecycleService _appLifecycleService;
    private readonly List<GesturePoint> _recordedGesturePoints = [];
    private bool _isRecordingGesture;

    public SettingsWindow(SettingsViewModel viewModel, AppLifecycleService appLifecycleService)
    {
        _appLifecycleService = appLifecycleService;
        InitializeComponent();
        DataContext = viewModel;
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_appLifecycleService.IsExplicitExit)
        {
            e.Cancel = true;
            Hide();
            return;
        }

        base.OnClosing(e);
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            return;
        }

        DragMove();
    }

    private void WindowBackground_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left || e.ClickCount != 1 || !CanDragFrom(e.OriginalSource as DependencyObject))
        {
            return;
        }

        try
        {
            DragMove();
        }
        catch (InvalidOperationException)
        {
        }
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void UpdateButton_Click(object sender, RoutedEventArgs e)
    {
        _appLifecycleService.OpenLatestReleasePage();
    }

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

    private static bool CanDragFrom(DependencyObject? source)
    {
        while (source is not null)
        {
            if (source is WpfButtonBase
                or WpfTextBoxBase
                or WpfSelector
                or WpfScrollBar
                or ScrollViewer
                or Hyperlink)
            {
                return false;
            }

            source = VisualTreeHelper.GetParent(source);
        }

        return true;
    }
}
