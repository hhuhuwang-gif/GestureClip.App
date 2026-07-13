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
    private readonly IWorkstationDashboardService _dashboardService;
    private readonly object _syncRoot = new();

    private Timer? _timer;
    private DateOnly? _mutedDate;
    private DateTimeOffset _nextAllowedReminderAt = DateTimeOffset.MinValue;
    private WorkTimeStage? _lastStageReminder;
    private DateTimeOffset _lastStageReminderAt = DateTimeOffset.MinValue;

    public OverworkReminderService(
        ISettingsService settingsService,
        IWorkTimeStageService stageService,
        IOverworkReminderToastService toastService,
        IWorkstationDashboardService dashboardService)
    {
        _settingsService = settingsService;
        _stageService = stageService;
        _toastService = toastService;
        _dashboardService = dashboardService;
    }

    public OverworkReminderService(
        ISettingsService settingsService,
        IWorkTimeStageService stageService,
        IOverworkReminderToastService toastService)
        : this(settingsService, stageService, toastService, NullWorkstationDashboardService.Instance)
    {
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

        if (IsMutedTodayFromSettings(now) || IsSnoozedFromSettings(now))
        {
            return;
        }

        if (now < _nextAllowedReminderAt)
        {
            return;
        }

        var maxPerDay = Math.Clamp(_settingsService.Get(SettingKeys.WorkstationOverworkReminderMaxPerDay, 4), 1, 12);
        var maxPerWeek = Math.Clamp(_settingsService.Get(SettingKeys.WorkstationOverworkReminderMaxPerWeek, 16), 1, 40);
        try
        {
            var dashboard = await _dashboardService.GetSnapshotAsync(now, cancellationToken);
            if (dashboard.RestReminderCount >= maxPerDay)
            {
                return;
            }
        }
        catch
        {
            // Snapshot is best-effort; do not block reminder path if stats fail.
        }

        if (GetWeekCount(now) >= maxPerWeek)
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
        await _dashboardService.RecordOverworkReminderAsync(now, cancellationToken);
        await IncrementWeekCountAsync(now, cancellationToken);
        ApplyToastResult(result, now);
    }

    private OverworkReminderNotification? BuildNotification(WorkTimeStageSnapshot snapshot, DateTimeOffset now)
    {
        var interval = Math.Clamp(_settingsService.Get(SettingKeys.WorkstationOverworkReminderIntervalMinutes, 60), 30, 180);
        var highRiskAfterHours = Math.Clamp(_settingsService.Get(SettingKeys.WorkstationOverworkHighRiskAfterHours, 8d), 6d, 12d);
        var minContinuousMinutes = Math.Clamp(
            _settingsService.Get(SettingKeys.WorkstationOverworkReminderMinContinuousMinutes, 45),
            15,
            180);
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

        // Only remind in early/mid work after continuous work threshold + interval cadence.
        if (snapshot.Stage is WorkTimeStage.EarlyWork or WorkTimeStage.MidWork &&
            snapshot.EffectiveWorkedTime.TotalMinutes >= Math.Max(interval, minContinuousMinutes))
        {
            return new OverworkReminderNotification(
                title,
                $"已连续工作约 {(int)snapshot.EffectiveWorkedTime.TotalMinutes} 分钟，建议休息 3 分钟。",
                "起来走两步，喝口水，看远处 20 秒。",
                snapshot.Stage,
                canSnooze);
        }

        return null;
    }

    private int GetWeekCount(DateTimeOffset now)
    {
        var key = GetIsoWeekKey(now);
        var storedKey = _settingsService.Get(SettingKeys.WorkBearRestReminderWeekKey, string.Empty);
        if (!string.Equals(storedKey, key, StringComparison.Ordinal))
        {
            return 0;
        }

        return Math.Max(0, _settingsService.Get(SettingKeys.WorkBearRestReminderWeekCount, 0));
    }

    private async Task IncrementWeekCountAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var key = GetIsoWeekKey(now);
        var storedKey = _settingsService.Get(SettingKeys.WorkBearRestReminderWeekKey, string.Empty);
        var count = string.Equals(storedKey, key, StringComparison.Ordinal)
            ? Math.Max(0, _settingsService.Get(SettingKeys.WorkBearRestReminderWeekCount, 0))
            : 0;
        count++;
        await _settingsService.SetAsync(SettingKeys.WorkBearRestReminderWeekKey, key, cancellationToken);
        await _settingsService.SetAsync(SettingKeys.WorkBearRestReminderWeekCount, count, cancellationToken);
    }

    private static string GetIsoWeekKey(DateTimeOffset now)
    {
        var date = DateOnly.FromDateTime(now.Date);
        var week = System.Globalization.ISOWeek.GetWeekOfYear(date.ToDateTime(TimeOnly.MinValue));
        var year = System.Globalization.ISOWeek.GetYear(date.ToDateTime(TimeOnly.MinValue));
        return $"{year:D4}-W{week:D2}";
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
                    _ = SnoozeAsync(now, CancellationToken.None);
                    break;
                case OverworkReminderToastResult.MuteToday:
                    _mutedDate = DateOnly.FromDateTime(now.Date);
                    _ = MuteTodayAsync(now, CancellationToken.None);
                    break;
                default:
                    var interval = Math.Clamp(_settingsService.Get(SettingKeys.WorkstationOverworkReminderIntervalMinutes, 60), 30, 180);
                    _nextAllowedReminderAt = now.AddMinutes(interval);
                    break;
            }
        }
    }

    public Task SnoozeAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var snoozeMinutes = Math.Clamp(_settingsService.Get(SettingKeys.WorkstationOverworkSnoozeMinutes, 15), 5, 60);
        var snoozedUntil = now.AddMinutes(snoozeMinutes);
        lock (_syncRoot)
        {
            _nextAllowedReminderAt = snoozedUntil;
        }

        return _settingsService.SetAsync(SettingKeys.WorkBearRestReminderSnoozedUntil, snoozedUntil.ToString("O"), cancellationToken);
    }

    public Task MuteTodayAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var date = DateOnly.FromDateTime(now.Date);
        lock (_syncRoot)
        {
            _mutedDate = date;
        }

        return _settingsService.SetAsync(SettingKeys.WorkBearRestReminderMutedDate, date.ToString("yyyy-MM-dd"), cancellationToken);
    }

    private bool IsMutedTodayFromSettings(DateTimeOffset now)
    {
        var mutedDate = _settingsService.Get(SettingKeys.WorkBearRestReminderMutedDate, string.Empty);
        return DateOnly.TryParse(mutedDate, out var date) && date == DateOnly.FromDateTime(now.Date);
    }

    private bool IsSnoozedFromSettings(DateTimeOffset now)
    {
        var snoozedUntil = _settingsService.Get(SettingKeys.WorkBearRestReminderSnoozedUntil, string.Empty);
        return DateTimeOffset.TryParse(snoozedUntil, out var until) && until > now;
    }

    public void Dispose()
    {
        _timer?.Dispose();
        _timer = null;
    }

    private sealed class NullWorkstationDashboardService : IWorkstationDashboardService
    {
        public static NullWorkstationDashboardService Instance { get; } = new();

        public Task<WorkstationDashboardSnapshot> GetSnapshotAsync(DateTimeOffset now, CancellationToken cancellationToken) =>
            Task.FromResult(new WorkstationDashboardSnapshot("工位小熊", "", TimeSpan.Zero, 0, 0, 0, false, TimeSpan.Zero, 0, 0, 0, 0, 0, 0, ""));

        public Task StartFishingAsync(DateTimeOffset now, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task EndFishingAsync(DateTimeOffset now, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task ResetTodayAsync(DateOnly date, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task RecordCopyAsync(DateTimeOffset now, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task RecordPasteAsync(DateTimeOffset now, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task RecordGestureAsync(DateTimeOffset now, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task RecordClipboardOpenAsync(DateTimeOffset now, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task RecordOverworkReminderAsync(DateTimeOffset now, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task SetSprintModeAsync(bool enabled, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task ClearTodayFishingAsync(DateOnly date, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task SnoozeAsync(DateTimeOffset now, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task MuteTodayAsync(DateTimeOffset now, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<WorkBearDailyReport> GenerateDailyReportAsync(DateTimeOffset now, CancellationToken cancellationToken) =>
            Task.FromResult(new WorkBearDailyReport(DateOnly.FromDateTime(now.Date), TimeSpan.Zero, TimeSpan.Zero, 0, TimeSpan.Zero, 0, 0, 0, 0, 0, 0, 0, TimeSpan.Zero, "", "", ""));
        public Task<string> GeneratePeriodReportAsync(DateTimeOffset now, int dayCount, CancellationToken cancellationToken) =>
            Task.FromResult("暂无周期报告。");
    }
}
