using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using GestureClip.App.Services;
using GestureClip.App.ViewModels;
using GestureClip.Core.Abstractions;

namespace GestureClip.App;

public partial class WorkstationDashboardWindow : Window
{
    private readonly WorkstationDashboardViewModel _viewModel;
    private readonly IAppLifecycleService _appLifecycleService;
    private readonly AppThemeService _themeService;
    private readonly DispatcherTimer _refreshTimer;
    private readonly DispatcherTimer _tickTimer;
    private bool _micaApplied;

    public WorkstationDashboardWindow(
        WorkstationDashboardViewModel viewModel,
        IAppLifecycleService appLifecycleService,
        AppThemeService themeService)
    {
        _viewModel = viewModel;
        _appLifecycleService = appLifecycleService;
        _themeService = themeService;
        InitializeComponent();
        DataContext = viewModel;
        _themeService.Changed += ThemeService_Changed;
        _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _refreshTimer.Tick += async (_, _) => await _viewModel.RefreshAsync();
        // Per-second live tick: extrapolates earnings/countdown locally, no DB access.
        _tickTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _tickTimer.Tick += (_, _) => _viewModel.TickRealtime();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        _micaApplied = WindowBackdrop.TryApplyMica(this, _themeService.Mode == AppUiThemeMode.Dark);
        if (_micaApplied)
        {
            // Translucent wash lets the mica material read through the content surface.
            RootBorder.SetResourceReference(BackgroundProperty, "BrushBackdropWash");
        }
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        PlayOpenAnimation();
        await _viewModel.RefreshAsync();
        _refreshTimer.Start();
        _tickTimer.Start();
    }

    private void Window_Closing(object sender, CancelEventArgs e)
    {
        _refreshTimer.Stop();
        _tickTimer.Stop();
        _themeService.Changed -= ThemeService_Changed;
    }

    private void ThemeService_Changed(object? sender, EventArgs e)
    {
        if (_micaApplied)
        {
            WindowBackdrop.UpdateDarkMode(this, _themeService.Mode == AppUiThemeMode.Dark);
        }
    }

    private void PlayOpenAnimation()
    {
        var fade = new DoubleAnimation
        {
            From = 0,
            To = 1,
            Duration = TimeSpan.FromMilliseconds(240),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        BeginAnimation(OpacityProperty, fade);
    }

    private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Escape)
        {
            Close();
            e.Handled = true;
        }
    }

    private void WindowBackground_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
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

    private void OpenSettingsButton_Click(object sender, RoutedEventArgs e)
    {
        _appLifecycleService.ShowSettingsWindow();
    }

    private void ToggleWidgetButton_Click(object sender, RoutedEventArgs e)
    {
        _appLifecycleService.ToggleWorkBearWidget();
    }
}
