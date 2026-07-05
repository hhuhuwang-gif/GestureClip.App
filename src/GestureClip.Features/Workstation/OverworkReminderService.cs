using GestureClip.Core.Abstractions;
using GestureClip.Core.Settings;
using GestureClip.Core.Workstation;

namespace GestureClip.Features.Workstation;

public sealed class OverworkReminderService : IOverworkReminderService, IDisposable
{
    private static readonly TimeSpan TimerInterval = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan StageReminderCooldown = TimeSpan.FromMinutes(60);

    private readonly ISettingsService _settingsService;
    private readonly IWorkTimeStageService _stageService;
    private readonly IOverworkReminderToastService _toastService;
    private readonly object _syncRoot = new();

    private Timer? _timer;
    private DateOnly? _mutedDate;
    private DateTimeOffset _nextAllowedReminderAt = DateTimeOffset.MinValue;
    private WorkTimeStage? _lastStageReminder;
    private DateTimeOffset _lastStageReminderAt = DateTimeOffset.MinValue;

    public OverworkReminderService(
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
        if (!_settingsService.Get(SettingKeys.WorkstationEnableOverworkReminder, true))
        {
            return;
        }

        if (_mutedDate == DateOnly.FromDateTime(now.Date))
        {
            return;
        }

        if (now < _nextAllowedReminderAt)
        {
            return;
        }

        var snapshot = _stageService.GetSnapshot(now);
        var notification = BuildNotification(snapshot, now);
        if (notification is null)
        {
            return;
        }

        var result = await _toastService.ShowAsync(notification, cancellationToken);
        ApplyToastResult(result, now);
    }

    private OverworkReminderNotification? BuildNotification(WorkTimeStageSnapshot snapshot, DateTimeOffset now)
    {
        var interval = Math.Clamp(_settingsService.Get(SettingKeys.WorkstationOverworkReminderIntervalMinutes, 60), 30, 180);
        var highRiskAfterHours = Math.Clamp(_settingsService.Get(SettingKeys.WorkstationOverworkHighRiskAfterHours, 8d), 6d, 12d);
        var canSnooze = _settingsService.Get(SettingKeys.WorkstationOverworkReminderCanSnooze, true);
        var strongWarning = _settingsService.Get(SettingKeys.WorkstationEnableStrongOverworkWarning, false);
        var title = "工位小熊提醒";

        if (snapshot.EffectiveWorkedTime.TotalHours >= highRiskAfterHours)
        {
            return new OverworkReminderNotification(
                title,
                strongWarning ? "连续工作时间已经很长，建议现在就停一下。" : "连续工作时间过长，建议立即休息。",
                strongWarning ? "保存文件，站起来活动 5 分钟，回来再继续。" : "保存文件，离开屏幕活动 5 分钟。",
                snapshot.Stage,
                canSnooze);
        }

        if (snapshot.Stage == WorkTimeStage.Overtime)
        {
            if (!CanShowStageReminder(snapshot.Stage, now, TimeSpan.FromMinutes(Math.Max(30, interval))))
            {
                return null;
            }

            MarkStageReminder(snapshot.Stage, now);
            return new OverworkReminderNotification(title, "已进入加班时间，建议安排收尾。", "你已经下班了，但鼠标还没下班。", snapshot.Stage, canSnooze);
        }

        if (snapshot.Stage == WorkTimeStage.LateWork)
        {
            if (!CanShowStageReminder(snapshot.Stage, now, StageReminderCooldown))
            {
                return null;
            }

            MarkStageReminder(snapshot.Stage, now);
            return new OverworkReminderNotification(title, "今日工作已进入后半程，注意补水。", "快到下班线了，别把自己跑冒烟。", snapshot.Stage, canSnooze);
        }

        if (snapshot.Stage is WorkTimeStage.EarlyWork or WorkTimeStage.MidWork && snapshot.EffectiveWorkedTime.TotalMinutes >= interval)
        {
            return new OverworkReminderNotification(
                title,
                $"已连续工作 {interval} 分钟，建议休息 3 分钟。",
                "起来走两步，喝口水，看远处 20 秒。",
                snapshot.Stage,
                canSnooze);
        }

        return null;
    }

    private bool CanShowStageReminder(WorkTimeStage stage, DateTimeOffset now, TimeSpan cooldown)
    {
        return _lastStageReminder != stage || now - _lastStageReminderAt >= cooldown;
    }

    private void MarkStageReminder(WorkTimeStage stage, DateTimeOffset now)
    {
        _lastStageReminder = stage;
        _lastStageReminderAt = now;
    }

    private void ApplyToastResult(OverworkReminderToastResult result, DateTimeOffset now)
    {
        lock (_syncRoot)
        {
            switch (result)
            {
                case OverworkReminderToastResult.Snooze:
                    var snoozeMinutes = Math.Clamp(_settingsService.Get(SettingKeys.WorkstationOverworkSnoozeMinutes, 15), 5, 60);
                    _nextAllowedReminderAt = now.AddMinutes(snoozeMinutes);
                    break;
                case OverworkReminderToastResult.MuteToday:
                    _mutedDate = DateOnly.FromDateTime(now.Date);
                    break;
                default:
                    var interval = Math.Clamp(_settingsService.Get(SettingKeys.WorkstationOverworkReminderIntervalMinutes, 60), 30, 180);
                    _nextAllowedReminderAt = now.AddMinutes(interval);
                    break;
            }
        }
    }

    public void Dispose()
    {
        _timer?.Dispose();
        _timer = null;
    }
}
