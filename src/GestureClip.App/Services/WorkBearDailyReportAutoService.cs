using GestureClip.Core.Abstractions;
using GestureClip.Core.Settings;
using GestureClip.Core.Workstation;
using Microsoft.Extensions.Logging;

namespace GestureClip.App.Services;

public sealed class WorkBearDailyReportAutoService : IDisposable
{
    private static readonly TimeSpan TimerInterval = TimeSpan.FromMinutes(1);

    private readonly ISettingsService _settingsService;
    private readonly IWorkstationDashboardService _dashboardService;
    private readonly TrayIconService _trayIconService;
    private readonly ILogger<WorkBearDailyReportAutoService> _logger;
    private readonly SemaphoreSlim _checkLock = new(1, 1);
    private System.Threading.Timer? _timer;

    public WorkBearDailyReportAutoService(
        ISettingsService settingsService,
        IWorkstationDashboardService dashboardService,
        TrayIconService trayIconService,
        ILogger<WorkBearDailyReportAutoService> logger)
    {
        _settingsService = settingsService;
        _dashboardService = dashboardService;
        _trayIconService = trayIconService;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (_timer is not null)
        {
            return Task.CompletedTask;
        }

        _timer = new System.Threading.Timer(_ => _ = CheckNowAsync(DateTimeOffset.Now, CancellationToken.None), null, TimeSpan.FromSeconds(30), TimerInterval);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _timer?.Dispose();
        _timer = null;
        return Task.CompletedTask;
    }

    public async Task CheckNowAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (!await _checkLock.WaitAsync(0, cancellationToken))
        {
            return;
        }

        try
        {
            if (!_settingsService.Get(SettingKeys.WorkstationEnabled, true) ||
                !_settingsService.Get(SettingKeys.EnableWorkBearHub, true) ||
                !_settingsService.Get(SettingKeys.EnableWorkReport, true) ||
                !_settingsService.Get(SettingKeys.AutoShowDailyWorkReport, true))
            {
                return;
            }

            var today = DateOnly.FromDateTime(now.Date);
            if (IsDateSetting(SettingKeys.WorkBearDailyReportLastShownDate, today) ||
                IsDateSetting(SettingKeys.WorkBearDailyReportMutedDate, today))
            {
                return;
            }

            var snapshot = await _dashboardService.GetSnapshotAsync(now, cancellationToken);
            if (!ShouldShowAfterWork(snapshot))
            {
                return;
            }

            var report = await _dashboardService.GenerateDailyReportAsync(now, cancellationToken);
            await _settingsService.SetAsync(SettingKeys.WorkBearDailyReportLastShownDate, today.ToString("yyyy-MM-dd"), cancellationToken);
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                _trayIconService.ShowWorkBearBalloon(
                    "工位小熊今日报告",
                    $"今日已赚 ￥{report.TodayEarned:0.00}，少点 {report.EstimatedSavedClicks} 次，评级：{report.Rating}。报告不含剪贴板内容。");
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to auto-show WorkBear daily report.");
        }
        finally
        {
            _checkLock.Release();
        }
    }

    private bool IsDateSetting(string key, DateOnly today)
    {
        var value = _settingsService.Get(key, string.Empty);
        return DateOnly.TryParse(value, out var date) && date == today;
    }

    private static bool ShouldShowAfterWork(WorkstationDashboardSnapshot snapshot)
    {
        return snapshot.TodayWorkDuration > TimeSpan.Zero &&
               snapshot.TimeUntilOffWork <= TimeSpan.Zero &&
               snapshot.WorkTimeStage is WorkTimeStage.OffWork or WorkTimeStage.Overtime;
    }

    public void Dispose()
    {
        _timer?.Dispose();
        _timer = null;
        _checkLock.Dispose();
    }
}
