using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using GestureClip.Features.Macros;

namespace GestureClip.App.Services;

public sealed partial class MacroRecorderService
{
    private const int TimerIntervalMilliseconds = 250;
    private Window? _host;
    private nint _hostHandle;
    private MacroWindowHotkeys? _hotkeys;
    private DispatcherTimer? _timer;
    private MacroPlaybackStatusWindow? _feedback;
    private TimeSpan _playDuration;
    private MacroPlaybackProgress _playProgress = new();
    private int _playEventCount;
    private string _idleHeading = "准备就绪";

    public string PlaybackTime => $"{_watch.Elapsed:hh\\:mm\\:ss} / {_playDuration:hh\\:mm\\:ss} · {_playProgress.CompletedEvents}/{_playEventCount} 项";
    public double PlaybackPercent => _playEventCount == 0 ? 0
        : (double)_playProgress.CompletedEvents / _playEventCount * 100;

    public void AttachHost(Window host)
    {
        if (ReferenceEquals(host, _host)) return;
        ReleaseHost();
        _host = host;
        _hostHandle = new WindowInteropHelper(host).EnsureHandle();
        _host.Closed += HostClosed;
        try { _hotkeys = new MacroWindowHotkeys(_hostHandle, this); }
        catch (Exception error) { ReportError(error); }
        _ = ExecuteAsync(RefreshFilesAsync);
        UpdateState();
    }

    private async void HostClosed(object? sender, EventArgs e)
    {
        ReleaseHost();
        await StopAndSaveAsync();
    }

    private void ReleaseHost()
    {
        if (_host is not null) _host.Closed -= HostClosed;
        _host = null;
        _hostHandle = 0;
        _hotkeys?.Dispose();
        _hotkeys = null;
        _timer?.Stop();
    }

    private void UpdateTimer()
    {
        if (!_recording && !_playing) { _timer?.Stop(); return; }
        if (_timer is null)
        {
            _timer = new DispatcherTimer(DispatcherPriority.Background)
                { Interval = TimeSpan.FromMilliseconds(TimerIntervalMilliseconds) };
            _timer.Tick += (_, _) => Tick();
        }
        _timer.Start();
    }

    private void ShowPlaybackFeedback()
    {
        _feedback = new MacroPlaybackStatusWindow(this);
        _feedback.Show();
    }

    private void ClosePlaybackFeedback()
    {
        _feedback?.Close();
        _feedback = null;
    }
}
