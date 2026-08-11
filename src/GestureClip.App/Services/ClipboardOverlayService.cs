using System.Diagnostics;
using System.Windows;
using GestureClip.Core.Abstractions;
using GestureClip.Core.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace GestureClip.App.Services;

public sealed class ClipboardOverlayService : IClipboardOverlayService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IWorkstationDashboardService _workstationDashboardService;
    private readonly ILogger<ClipboardOverlayService> _logger;
    private readonly bool _perfLogEnabled;
    private ClipboardOverlayWindow? _window;

    public ClipboardOverlayService(
        IServiceProvider serviceProvider,
        ISettingsService settingsService,
        IWorkstationDashboardService workstationDashboardService,
        ILogger<ClipboardOverlayService> logger)
    {
        _serviceProvider = serviceProvider;
        _workstationDashboardService = workstationDashboardService;
        _logger = logger;
        _perfLogEnabled = settingsService.Get(SettingKeys.ClipboardPerfLogEnabled, false) ||
            settingsService.Get(SettingKeys.GestureDebugEnabled, false);
    }

    public async Task ShowAsync()
    {
        await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
        {
            var window = EnsureWindow();
            window.Show();
            window.Activate();
            window.FocusSearchBox();
            RecordOpenInBackground();
            _ = LoadHistoryWithPerfAsync(window);
        });
    }

    public async Task ToggleAsync()
    {
        await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
        {
            var window = EnsureWindow();
            if (window.IsVisible)
            {
                window.Hide();
                return;
            }

            window.Show();
            window.Activate();
            window.FocusSearchBox();
            RecordOpenInBackground();
            _ = LoadHistoryWithPerfAsync(window);
        });
    }

    private void RecordOpenInBackground()
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await _workstationDashboardService.RecordClipboardOpenAsync(DateTimeOffset.UtcNow, CancellationToken.None);
            }
            catch
            {
                // optional local stat; overlay must stay fast.
            }
        });
    }

    public async Task RefreshAsync()
    {
        await System.Windows.Application.Current.Dispatcher.InvokeAsync(async () =>
        {
            if (_window is not null)
            {
                await _window.LoadHistoryAsync();
            }
        });
    }

    private ClipboardOverlayWindow EnsureWindow()
    {
        if (_window is not null)
        {
            return _window;
        }

        _window = _serviceProvider.GetRequiredService<ClipboardOverlayWindow>();
        _window.Closed += (_, _) => _window = null;
        return _window;
    }

    private async Task LoadHistoryWithPerfAsync(ClipboardOverlayWindow window)
    {
        var watch = Stopwatch.StartNew();
        try
        {
            await window.LoadHistoryAsync();
        }
        finally
        {
            watch.Stop();
            if (_perfLogEnabled)
            {
                _logger.LogInformation("ClipboardPerf OverlayLoadMs ElapsedMs={ElapsedMs}", watch.ElapsedMilliseconds);
            }
        }
    }
}
