using System.Windows;
using System.Windows.Threading;
using GestureClip.App.Diagnostics;

namespace GestureClip.App;

public partial class WorkstationDashboardWindow
{
    private static readonly TimeSpan SnapshotRefreshInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan RealtimeTickInterval = TimeSpan.FromSeconds(1);
    private readonly DispatcherTimer _refreshTimer = new(DispatcherPriority.Background) { Interval = SnapshotRefreshInterval };
    private readonly DispatcherTimer _tickTimer = new(DispatcherPriority.Background) { Interval = RealtimeTickInterval };
    private bool _hasLoaded;
    private bool _isClosing;
    private bool _refreshInProgress;

    private bool IsRefreshActive => _hasLoaded && !_isClosing && IsVisible && WindowState != WindowState.Minimized;

    private void InitializeRefreshTimers()
    {
        _refreshTimer.Tick += RefreshTimerTick;
        _tickTimer.Tick += RealtimeTimerTick;
        IsVisibleChanged += WindowPresentationChanged;
        StateChanged += WindowStateChanged;
    }

    private async void RefreshTimerTick(object? sender, EventArgs e) => await RefreshAndResumeAsync();

    private void RealtimeTimerTick(object? sender, EventArgs e) => _viewModel.TickRealtime();

    private async void WindowPresentationChanged(object sender, DependencyPropertyChangedEventArgs e) => await UpdateRefreshActivityAsync();

    private async void WindowStateChanged(object? sender, EventArgs e) => await UpdateRefreshActivityAsync();

    private async Task UpdateRefreshActivityAsync()
    {
        if (!IsRefreshActive)
        {
            StopRefreshTimers();
            UiTrace.Report("Hub refresh paused: hidden or minimized");
            return;
        }
        await RefreshAndResumeAsync();
    }

    private async Task RefreshAndResumeAsync()
    {
        if (!IsRefreshActive || _refreshInProgress) return;
        _refreshInProgress = true;
        _refreshTimer.Stop();
        try
        {
            await _viewModel.RefreshAsync();
        }
        finally
        {
            _refreshInProgress = false;
            if (IsRefreshActive)
            {
                _refreshTimer.Start();
                _tickTimer.Start();
            }
        }
    }

    private void StopRefreshTimers()
    {
        _refreshTimer.Stop();
        _tickTimer.Stop();
    }
}
