using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using GestureClip.App.ViewModels;
using GestureClip.Core.Abstractions;

namespace GestureClip.App;

public partial class WorkstationDashboardWindow : Window
{
    private readonly WorkstationDashboardViewModel _viewModel;
    private readonly IAppLifecycleService _appLifecycleService;
    private readonly DispatcherTimer _refreshTimer;
    private string _lastEarned = "";
    private string _lastOffWork = "";

    public WorkstationDashboardWindow(WorkstationDashboardViewModel viewModel, IAppLifecycleService appLifecycleService)
    {
        _viewModel = viewModel;
        _appLifecycleService = appLifecycleService;
        InitializeComponent();
        DataContext = viewModel;
        _viewModel.PropertyChanged += ViewModel_PropertyChanged;
        _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _refreshTimer.Tick += async (_, _) => await _viewModel.RefreshAsync();
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        await _viewModel.RefreshAsync();
        _lastEarned = _viewModel.TodayEarnedText;
        _lastOffWork = _viewModel.OffWorkCountdownText;
        _refreshTimer.Start();
    }

    private void Window_Closing(object sender, CancelEventArgs e)
    {
        _refreshTimer.Stop();
        _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
    }

    private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Escape)
        {
            Close();
            e.Handled = true;
        }
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(WorkstationDashboardViewModel.TodayEarnedText)
            or nameof(WorkstationDashboardViewModel.OffWorkCountdownText))
        {
            FlashMetricIfChanged();
        }
    }

    private void FlashMetricIfChanged()
    {
        if (!IsLoaded)
        {
            return;
        }

        if (!string.Equals(_lastEarned, _viewModel.TodayEarnedText, StringComparison.Ordinal)
            && TodayEarnedMetric is not null)
        {
            _lastEarned = _viewModel.TodayEarnedText;
            FlashElement(TodayEarnedMetric);
        }

        if (!string.Equals(_lastOffWork, _viewModel.OffWorkCountdownText, StringComparison.Ordinal)
            && OffWorkMetric is not null)
        {
            _lastOffWork = _viewModel.OffWorkCountdownText;
            FlashElement(OffWorkMetric);
        }
    }

    private static void FlashElement(UIElement element)
    {
        var animation = new DoubleAnimation
        {
            From = 0.55,
            To = 1,
            Duration = TimeSpan.FromMilliseconds(180)
        };
        element.BeginAnimation(UIElement.OpacityProperty, animation);
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
}
