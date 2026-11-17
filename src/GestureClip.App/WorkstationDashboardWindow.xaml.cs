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
        InitializeRefreshTimers();
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
        _hasLoaded = true;
        await RefreshAndResumeAsync();
    }

    private void Window_Closing(object sender, CancelEventArgs e)
    {
        _isClosing = true;
        StopRefreshTimers();
        _themeService.Changed -= ThemeService_Changed;
        IsVisibleChanged -= WindowPresentationChanged;
        StateChanged -= WindowStateChanged;
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
        if (!SystemParameters.ClientAreaAnimation) return;
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
