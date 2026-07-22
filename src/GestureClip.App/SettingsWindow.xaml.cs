using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using GestureClip.App.Controls;
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
    private const int GwlStyle = -16;
    private const int WsSysmenu = 0x00080000;
    private const int WsMinimizebox = 0x00020000;

    private readonly AppLifecycleService _appLifecycleService;
    private readonly List<GesturePoint> _recordedGesturePoints = [];
    private bool _isRecordingGesture;

    public SettingsWindow(SettingsViewModel viewModel, AppLifecycleService appLifecycleService)
    {
        _appLifecycleService = appLifecycleService;
        InitializeComponent();
        DataContext = viewModel;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        EnableTaskbarMinimizeBehavior();
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

        try
        {
            DragMove();
            e.Handled = true;
        }
        catch (InvalidOperationException)
        {
        }
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
            e.Handled = true;
        }
        catch (InvalidOperationException)
        {
        }
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void MaximizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private async void UpdateButton_Click(object sender, RoutedEventArgs e)
    {
        await _appLifecycleService.StartCoverUpdateAsync();
    }

    private async void CheckUpdateButton_Click(object sender, RoutedEventArgs e)
    {
        await _appLifecycleService.CheckForUpdatesAsync();
    }

    private void OpenReleaseButton_Click(object sender, RoutedEventArgs e)
    {
        _appLifecycleService.OpenLatestReleasePage();
    }

    private void ScrollToCustomGestureDesigner_Click(object sender, RoutedEventArgs e)
    {
        NavigateToPage("bindings");
        Dispatcher.BeginInvoke(new Action(() =>
        {
            try
            {
                var target = GestureDesignerPanel.TransformToAncestor(GestureBindingPageScrollViewer)
                    .Transform(new System.Windows.Point(0, 0));
                GestureBindingPageScrollViewer.ScrollToVerticalOffset(
                    Math.Max(0, GestureBindingPageScrollViewer.VerticalOffset + target.Y - 24));
            }
            catch (InvalidOperationException)
            {
                GestureBindingPageScrollViewer.ScrollToEnd();
            }
        }), System.Windows.Threading.DispatcherPriority.Loaded);
    }

    private void NavigateToBindings_Click(object sender, RoutedEventArgs e) => NavigateToPage("bindings");

    private void NavigateToEdgeEnhancement_Click(object sender, RoutedEventArgs e)
    {
        NavigateToPage("gestures");
        Dispatcher.BeginInvoke(new Action(() =>
        {
            GestureAdvancedSettingsExpander.IsExpanded = true;
            try
            {
                EdgeTriggerSettingsGroup.BringIntoView();
            }
            catch (InvalidOperationException)
            {
                EdgeEnhancementPromoCard.BringIntoView();
            }
        }), System.Windows.Threading.DispatcherPriority.Loaded);
    }

    private void ExpandGestureAdvanced_Click(object sender, RoutedEventArgs e)
    {
        NavigateToPage("gestures");
        Dispatcher.BeginInvoke(new Action(() =>
        {
            GestureAdvancedSettingsExpander.IsExpanded = true;
            GestureAdvancedSettingsExpander.BringIntoView();
        }), System.Windows.Threading.DispatcherPriority.Loaded);
    }

    private void FeatureMapCard_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string tag })
        {
            NavigateToPage(tag);
            e.Handled = true;
        }
    }

    private void ChangeGestureAction_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not SettingsViewModel viewModel)
        {
            return;
        }

        var pattern = (sender as FrameworkElement)?.Tag as string;
        if (string.IsNullOrWhiteSpace(pattern))
        {
            return;
        }

        viewModel.SelectedGestureBindingSelectionKey = pattern;
        NavigateToPage("bindings");
        Dispatcher.BeginInvoke(new Action(() =>
        {
            try
            {
                GestureBindingDetailPanel.BringIntoView();
                GestureActionEditorTitle.BringIntoView();
            }
            catch (InvalidOperationException)
            {
                GestureBindingPageScrollViewer.ScrollToVerticalOffset(
                    Math.Max(0, GestureBindingPageScrollViewer.ScrollableHeight * 0.45));
            }
        }), System.Windows.Threading.DispatcherPriority.Loaded);
        e.Handled = true;
    }

    /// <summary>
    /// Open a settings tab by header keyword / alias. Safe to call after show.
    /// </summary>
    public void NavigateToPage(string page)
    {
        if (MainSettingsTabControl is null)
        {
            return;
        }

        var key = (page ?? "").Trim().ToLowerInvariant();
        var header = key switch
        {
            "home" or "首页" or "" => "首页",
            "clipboard" or "剪贴板" or "smartpaste" or "智能粘贴" => "剪贴板",
            "gestures" or "手势" or "edge" or "边缘" => "手势",
            "bindings" or "动作" or "动作绑定" or "设计" or "designer" => "动作绑定",
            "privacy" or "隐私" or "数据" => "隐私",
            "startup" or "自启" => "自启",
            "workbear" or "小熊" or "工位" => "小熊",
            "diagnostics" or "诊断" => "诊断",
            "about" or "关于" => "关于",
            _ => "首页"
        };

        foreach (var item in MainSettingsTabControl.Items)
        {
            if (item is TabItem tab && string.Equals(tab.Header?.ToString(), header, StringComparison.Ordinal))
            {
                MainSettingsTabControl.SelectedItem = tab;
                break;
            }
        }
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

    private void EnableTaskbarMinimizeBehavior()
    {
        var handle = new WindowInteropHelper(this).Handle;
        if (handle == IntPtr.Zero)
        {
            return;
        }

        var style = GetWindowLong(handle, GwlStyle);
        SetWindowLong(handle, GwlStyle, style | WsSysmenu | WsMinimizebox);
    }

    private void PassMouseWheelToParent(object sender, MouseWheelEventArgs e)
    {
        if (e.Handled)
        {
            return;
        }

        e.Handled = true;
        GestureBindingPageScrollViewer.ScrollToVerticalOffset(
            GestureBindingPageScrollViewer.VerticalOffset - e.Delta);
    }

    private static bool CanDragFrom(DependencyObject? source)
    {
        while (source is not null)
        {
            if (source is WpfButtonBase
                or WpfTextBoxBase
                or WpfSelector
                or WpfScrollBar
                or TabItem
                or ComboBoxItem
                or ListBoxItem
                or Expander
                or ScrollViewer
                or Hyperlink
                or GesturePatternView)
            {
                return false;
            }

            source = VisualTreeHelper.GetParent(source);
        }

        return true;
    }

    private static T? FindAncestor<T>(DependencyObject? source)
        where T : DependencyObject
    {
        while (source is not null)
        {
            if (source is T target)
            {
                return target;
            }

            source = VisualTreeHelper.GetParent(source);
        }

        return null;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
}
