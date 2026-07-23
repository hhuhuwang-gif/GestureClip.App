using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;
using GestureClip.App.Controls;
using GestureClip.Core.Gestures;
using GestureClip.App.Services;
using GestureClip.App.ViewModels;
using GestureClip.Core.Abstractions;
using WpfButtonBase = System.Windows.Controls.Primitives.ButtonBase;
using WpfScrollBar = System.Windows.Controls.Primitives.ScrollBar;
using WpfSelector = System.Windows.Controls.Primitives.Selector;
using WpfTextBoxBase = System.Windows.Controls.Primitives.TextBoxBase;
using WpfRadioButton = System.Windows.Controls.RadioButton;
using WpfButton = System.Windows.Controls.Button;
using WpfHorizontalAlignment = System.Windows.HorizontalAlignment;
using WpfKeyEventArgs = System.Windows.Input.KeyEventArgs;
using WpfKey = System.Windows.Input.Key;
using WpfVisibility = System.Windows.Visibility;
using WpfThickness = System.Windows.Thickness;
using WpfFontWeights = System.Windows.FontWeights;
using WpfBrush = System.Windows.Media.Brush;

namespace GestureClip.App;

public partial class SettingsWindow : Window
{
    private const int GwlStyle = -16;
    private const int WsSysmenu = 0x00080000;
    private const int WsMinimizebox = 0x00020000;
    private const int WsThickframe = 0x00040000;
    private const int WsMaximizebox = 0x00010000;
    private const int WmNcHitTest = 0x0084;
    private const int HtClient = 1;
    private const int HtLeft = 10;
    private const int HtRight = 11;
    private const int HtTop = 12;
    private const int HtTopLeft = 13;
    private const int HtTopRight = 14;
    private const int HtBottom = 15;
    private const int HtBottomLeft = 16;
    private const int HtBottomRight = 17;
    private const int ResizeBorderThickness = 6;
    private HwndSource? _hwndSource;

    private readonly AppLifecycleService _appLifecycleService;
    private readonly List<GesturePoint> _recordedGesturePoints = [];
    private bool _isRecordingGesture;
    private bool _isRailCollapsed;
    private bool _suppressNavSync;
    private GridLength _savedRailWidth = new(240);

    public SettingsWindow(
        SettingsViewModel viewModel,
        AppLifecycleService appLifecycleService,
        ISettingsService settingsService)
    {
        _appLifecycleService = appLifecycleService;
        _settingsService = settingsService;
        InitializeComponent();
        DataContext = viewModel;
        RestoreWindowPlacement();
        Loaded += (_, _) => _windowPlacementReady = true;
        LocationChanged += (_, _) => SaveWindowPlacement();
        SizeChanged += (_, _) => SaveWindowPlacement();
        StateChanged += (_, _) => SaveWindowPlacement();
        Closed += (_, _) => SaveWindowPlacement();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        EnableTaskbarMinimizeBehavior();
        EnableBorderlessResize();
        SyncNavSelection("home");
        UpdateSearchPlaceholder();
        // Ensure template names are available for rail chevron
        ToggleRailButton?.ApplyTemplate();
        UpdateRailToggleGlyph(collapsed: false);
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
            MaximizeButton_Click(sender, e);
            e.Handled = true;
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

    /// <summary>
    /// Open a settings tab by header keyword / alias. Safe to call after show.
    /// </summary>
    public void NavigateToPage(string page, string? targetElementName = null)
    {
        if (MainSettingsTabControl is null)
        {
            return;
        }

        var key = NormalizePageKey(page);
        var header = PageHeaderFromKey(key);

        foreach (var item in MainSettingsTabControl.Items)
        {
            if (item is TabItem tab && string.Equals(tab.Header?.ToString(), header, StringComparison.Ordinal))
            {
                MainSettingsTabControl.SelectedItem = tab;
                break;
            }
        }

        SyncNavSelection(key);
        CloseSearchResults();

        if (!string.IsNullOrWhiteSpace(targetElementName))
        {
            Dispatcher.BeginInvoke(new Action(() => ScrollToNamedElement(targetElementName!)),
                System.Windows.Threading.DispatcherPriority.Loaded);
        }
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
