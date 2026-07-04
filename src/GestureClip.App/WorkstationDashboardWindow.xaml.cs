using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using GestureClip.App.ViewModels;
using GestureClip.Core.Abstractions;

namespace GestureClip.App;

public partial class WorkstationDashboardWindow : Window
{
    private readonly WorkstationDashboardViewModel _viewModel;
    private readonly IAppLifecycleService _appLifecycleService;
    private readonly DispatcherTimer _refreshTimer;

    public WorkstationDashboardWindow(WorkstationDashboardViewModel viewModel, IAppLifecycleService appLifecycleService)
    {
        _viewModel = viewModel;
        _appLifecycleService = appLifecycleService;
        InitializeComponent();
        DataContext = viewModel;
        _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _refreshTimer.Tick += async (_, _) => await _viewModel.RefreshAsync();
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        await _viewModel.RefreshAsync();
        _refreshTimer.Start();
    }

    private void Window_Closing(object sender, CancelEventArgs e)
    {
        _refreshTimer.Stop();
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

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void OpenSettingsButton_Click(object sender, RoutedEventArgs e)
    {
        _appLifecycleService.ShowSettingsWindow();
    }
}
