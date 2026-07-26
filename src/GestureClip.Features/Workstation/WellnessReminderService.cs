using GestureClip.Core.Abstractions;
using GestureClip.Core.Settings;
using GestureClip.Core.Workstation;

namespace GestureClip.Features.Workstation;

/// <summary>
/// Opt-in water / stretch toasts on their own cadences, shown only during work stages.
/// At most one wellness toast per check so the two types never stack.
/// </summary>
public sealed class WellnessReminderService : IWellnessReminderService, IDisposable
{
    private static readonly TimeSpan TimerInterval = TimeSpan.FromMinutes(1);

    private readonly ISettingsService _settingsService;
    private readonly IWorkTimeStageService _stageService;
    private readonly IOverworkReminderToastService _toastService;
    private readonly object _syncRoot = new();

    private Timer? _timer;
    private DateTimeOffset _lastWaterAt = DateTimeOffset.MinValue;
    private DateTimeOffset _lastStretchAt = DateTimeOffset.MinValue;

    public WellnessReminderService(
        ISettingsService settingsService,
        IWorkTimeStageService stageService,
        IOverworkReminderToastService toastService)
    {
        _settingsService = settingsService;
        _stageService = stageService;
        _toastService = toastService;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (_timer is not null)
        {
            return Task.CompletedTask;
        }

        // First reminder fires one full interval after start, not immediately.
        var now = DateTimeOffset.Now;
        _lastWaterAt = now;
        _lastStretchAt = now;
        _timer = new Timer(_ => _ = CheckNowAsync(DateTimeOffset.Now, CancellationToken.None), null, TimerInterval, TimerInterval);
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
        var stage = _stageService.GetSnapshot(now).Stage;
        if (stage is not (WorkTimeStage.EarlyWork or WorkTimeStage.MidWork or WorkTimeStage.LateWork or WorkTimeStage.Overtime))
        {
            return;
        }

        OverworkReminderNotification? notification = null;
        lock (_syncRoot)
        {
            if (_settingsService.Get(SettingKeys.WellnessWaterReminderEnabled, false) &&
                now - _lastWaterAt >= GetWaterInterval())
            {
                _lastWaterAt = now;
                notification = new OverworkReminderNotification(
                    "该喝水了 💧",
                    "工位小熊提醒你补充水分。",
                    "接一杯温水，顺便让眼睛离开屏幕 20 秒。",
                    stage,
                    CanSnooze: false);
            }
            else if (_settingsService.Get(SettingKeys.WellnessStretchReminderEnabled, false) &&
                now - _lastStretchAt >= GetStretchInterval())
            {
                _lastStretchAt = now;
                notification = new OverworkReminderNotification(
                    "起来拉伸一下 🧘",
                    "久坐了，站起来活动 2 分钟。",
                    "耸耸肩、转转脖子、伸个懒腰，回来更有劲。",
                    stage,
                    CanSnooze: false);
            }
        }

        if (notification is not null)
        {
            await _toastService.ShowAsync(notification, cancellationToken);
        }
    }

    public void Dispose()
    {
        _timer?.Dispose();
        _timer = null;
    }

    private TimeSpan GetWaterInterval() =>
        TimeSpan.FromMinutes(Math.Clamp(_settingsService.Get(SettingKeys.WellnessWaterIntervalMinutes, 60), 30, 180));

    private TimeSpan GetStretchInterval() =>
        TimeSpan.FromMinutes(Math.Clamp(_settingsService.Get(SettingKeys.WellnessStretchIntervalMinutes, 90), 30, 240));
}
