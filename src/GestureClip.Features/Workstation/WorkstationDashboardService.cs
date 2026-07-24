using System.Globalization;
using System.Text;
using GestureClip.Core.Abstractions;
using GestureClip.Core.Settings;
using GestureClip.Core.Workstation;

namespace GestureClip.Features.Workstation;

public sealed class WorkstationDashboardService : IWorkstationDashboardService
{
    private const int WorkdaysPerMonth = 22;
    private const int SavedClicksPerGesture = 3;

    private readonly ISettingsService _settingsService;
    private readonly IWorkstationStatsRepository _statsRepository;
    private readonly IWorkTimeStageService _stageService;

    public WorkstationDashboardService(
        ISettingsService settingsService,
        IWorkstationStatsRepository statsRepository,
        IWorkTimeStageService stageService)
    {
        _settingsService = settingsService;
        _statsRepository = statsRepository;
        _stageService = stageService;
    }

    public WorkstationDashboardService(
        ISettingsService settingsService,
        IWorkstationStatsRepository statsRepository)
        : this(settingsService, statsRepository, new WorkTimeStageService(settingsService))
    {
    }

    public async Task<WorkstationDashboardSnapshot> GetSnapshotAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var settings = GetSettings();
        var date = DateOnly.FromDateTime(now.Date);
        var stats = await _statsRepository.GetOrCreateAsync(date, cancellationToken);
        var stageSnapshot = _stageService.GetSnapshot(now);
        var textStyle = GetTextStyle();
        var minuteWage = GetMinuteWage(settings);
        var earnedMinutes = GetEffectiveWorkedMinutes(now, settings);
        var currentFishingMinutes = GetCurrentFishingMinutes(stats, now);
        var totalFishingMinutes = stats.FishingMinutes + currentFishingMinutes;
        var workDuration = GetGrossWorkDuration(now, settings);
        var overtime = GetOvertimeDuration(now, settings);
        var untilOffWork = GetTimeUntilOffWork(now, settings);
        var sprintActive = IsSprintActive(untilOffWork, stageSnapshot.Stage);
        var rating = WorkBearTextProvider.Rating(workDuration, TimeSpan.FromMinutes(totalFishingMinutes), overtime, stats.EstimatedSavedClicks);
        var report = BuildDailyReport(
            date,
            workDuration,
            TimeSpan.FromMinutes((double)earnedMinutes),
            earnedMinutes * minuteWage,
            TimeSpan.FromMinutes(totalFishingMinutes),
            totalFishingMinutes * minuteWage,
            stats,
            overtime,
            rating,
            WorkBearTextProvider.BearLine(stageSnapshot.Stage, textStyle, stats.FishingStartedAt is not null, untilOffWork, now));

        return new WorkstationDashboardSnapshot(
            "工位小熊",
            "坐在你电脑里的打工人状态 Hub",
            untilOffWork,
            earnedMinutes * minuteWage,
            GetMonthEarned(now, settings),
            GetDaysUntilPayday(now, settings.Payday),
            stats.FishingStartedAt is not null,
            TimeSpan.FromMinutes(currentFishingMinutes),
            currentFishingMinutes * minuteWage,
            totalFishingMinutes * minuteWage,
            stats.CopyCount,
            stats.PasteCount,
            stats.GestureCount,
            stats.EstimatedSavedClicks,
            stageSnapshot.Theme.ShortStatusText,
            stageSnapshot.Stage,
            WorkBearTextProvider.StageText(stageSnapshot.Stage),
            WorkBearTextProvider.BearStatus(stageSnapshot.Stage, stats.FishingStartedAt is not null),
            WorkBearTextProvider.BearLine(stageSnapshot.Stage, textStyle, stats.FishingStartedAt is not null, untilOffWork, now),
            minuteWage,
            TimeSpan.FromMinutes(totalFishingMinutes),
            stats.OpenClipboardCount,
            workDuration,
            TimeSpan.FromMinutes((double)earnedMinutes),
            stageSnapshot.EffectiveWorkedTime,
            GetNextRestReminderAt(now),
            stats.OverworkReminderCount,
            WorkBearTextProvider.RestRisk(stageSnapshot.EffectiveWorkedTime, stageSnapshot.Stage),
            IsRestReminderEnabledForToday(now),
            sprintActive,
            WorkBearTextProvider.SprintSuggestion(stageSnapshot.Stage, untilOffWork, textStyle),
            overtime,
            rating,
            report.ReportText,
            "仅供娱乐估算，不作财务或考勤依据；所有数据仅保存在本地。",
            stageSnapshot.Theme.AccentColor,
            stageSnapshot.Theme.StartColor,
            stageSnapshot.Theme.EndColor,
            stageSnapshot.WorkProgress,
            MapRestRiskLevel(WorkBearTextProvider.RestRisk(stageSnapshot.EffectiveWorkedTime, stageSnapshot.Stage)));
    }

    public async Task StartFishingAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (!IsEnabled() || !_settingsService.Get(SettingKeys.EnableFishMode, true))
        {
            return;
        }

        var stats = await _statsRepository.GetOrCreateAsync(DateOnly.FromDateTime(now.Date), cancellationToken);
        if (stats.FishingStartedAt is not null)
        {
            return;
        }

        await _statsRepository.SaveAsync(stats with { FishingStartedAt = now }, cancellationToken);
    }

    public async Task EndFishingAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (!IsEnabled())
        {
            return;
        }

        var stats = await _statsRepository.GetOrCreateAsync(DateOnly.FromDateTime(now.Date), cancellationToken);
        if (stats.FishingStartedAt is null)
        {
            return;
        }

        var minutes = GetCurrentFishingMinutes(stats, now);
        await _statsRepository.SaveAsync(stats with
        {
            FishingMinutes = stats.FishingMinutes + minutes,
            FishingStartedAt = null
        }, cancellationToken);
    }

    public Task ResetTodayAsync(DateOnly date, CancellationToken cancellationToken)
    {
        return _statsRepository.ResetAsync(date, cancellationToken);
    }

    public Task ClearTodayFishingAsync(DateOnly date, CancellationToken cancellationToken)
    {
        return ClearTodayFishingCoreAsync(date, cancellationToken);
    }

    private async Task ClearTodayFishingCoreAsync(DateOnly date, CancellationToken cancellationToken)
    {
        var stats = await _statsRepository.GetOrCreateAsync(date, cancellationToken);
        await _statsRepository.SaveAsync(stats with { FishingMinutes = 0, FishingStartedAt = null }, cancellationToken);
    }

    public async Task RecordCopyAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (!IsEnabled()) return;
        await _statsRepository.IncrementCountersAsync(DateOnly.FromDateTime(now.Date), 1, 0, 0, 0, cancellationToken);
    }

    public async Task RecordPasteAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (!IsEnabled()) return;
        await _statsRepository.IncrementCountersAsync(DateOnly.FromDateTime(now.Date), 0, 1, 0, 0, cancellationToken);
    }

    public async Task RecordGestureAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (!IsEnabled()) return;
        await _statsRepository.IncrementCountersAsync(DateOnly.FromDateTime(now.Date), 0, 0, 1, SavedClicksPerGesture, cancellationToken);
    }

    public async Task RecordClipboardOpenAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (!IsEnabled()) return;
        await _statsRepository.IncrementHubCountersAsync(DateOnly.FromDateTime(now.Date), 1, 0, cancellationToken);
    }

    public async Task RecordOverworkReminderAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (!IsEnabled()) return;
        await _statsRepository.IncrementHubCountersAsync(DateOnly.FromDateTime(now.Date), 0, 1, cancellationToken);
    }

    public Task SetSprintModeAsync(bool enabled, CancellationToken cancellationToken)
    {
        return _settingsService.SetAsync(SettingKeys.WorkBearSprintManualEnabled, enabled, cancellationToken);
    }

    public async Task<WorkBearDailyReport> GenerateDailyReportAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var settings = GetSettings();
        var date = DateOnly.FromDateTime(now.Date);
        var stats = await _statsRepository.GetOrCreateAsync(date, cancellationToken);
        var stage = _stageService.GetSnapshot(now).Stage;
        var minuteWage = GetMinuteWage(settings);
        var earnedMinutes = GetEffectiveWorkedMinutes(now, settings);
        var currentFishingMinutes = GetCurrentFishingMinutes(stats, now);
        var totalFishingMinutes = stats.FishingMinutes + currentFishingMinutes;
        var workDuration = GetGrossWorkDuration(now, settings);
        var overtime = GetOvertimeDuration(now, settings);
        var rating = WorkBearTextProvider.Rating(workDuration, TimeSpan.FromMinutes(totalFishingMinutes), overtime, stats.EstimatedSavedClicks);
        return BuildDailyReport(
            date,
            workDuration,
            TimeSpan.FromMinutes((double)earnedMinutes),
            earnedMinutes * minuteWage,
            TimeSpan.FromMinutes(totalFishingMinutes),
            totalFishingMinutes * minuteWage,
            stats,
            overtime,
            rating,
            WorkBearTextProvider.BearLine(stage, GetTextStyle(), stats.FishingStartedAt is not null, GetTimeUntilOffWork(now, settings), now));
    }

    public async Task<string> GeneratePeriodReportAsync(DateTimeOffset now, int dayCount, CancellationToken cancellationToken)
    {
        dayCount = Math.Clamp(dayCount, 1, 31);
        var end = DateOnly.FromDateTime(now.Date);
        var start = end.AddDays(1 - dayCount);
        var settings = GetSettings();
        var minuteWage = GetMinuteWage(settings);
        var totalEarned = 0m;
        var totalFishingMinutes = 0;
        var totalCopy = 0;
        var totalPaste = 0;
        var totalGesture = 0;
        var totalSaved = 0;
        var totalReminders = 0;
        var daysWithData = 0;

        for (var date = start; date <= end; date = date.AddDays(1))
        {
            var stats = await _statsRepository.GetOrCreateAsync(date, cancellationToken);
            var hasActivity = stats.CopyCount > 0 || stats.PasteCount > 0 || stats.GestureCount > 0 ||
                              stats.FishingMinutes > 0 || stats.EstimatedSavedClicks > 0;
            if (!hasActivity)
            {
                continue;
            }

            daysWithData++;
            // Approximate earned for historical days using settings; today uses live minutes.
            var earnedMinutes = date == end
                ? GetEffectiveWorkedMinutes(now, settings)
                : EstimateWorkdayMinutes(settings);
            totalEarned += earnedMinutes * minuteWage;
            totalFishingMinutes += stats.FishingMinutes;
            totalCopy += stats.CopyCount;
            totalPaste += stats.PasteCount;
            totalGesture += stats.GestureCount;
            totalSaved += stats.EstimatedSavedClicks;
            totalReminders += stats.OverworkReminderCount;
        }

        var label = dayCount <= 7 ? "本周" : "本月";
        return
            $"{label}本地总结（{start:MM/dd}–{end:MM/dd}）\n" +
            $"活跃天数：{daysWithData}\n" +
            $"估算收益：￥{totalEarned:F2}\n" +
            $"摸鱼：{totalFishingMinutes} 分钟（约 ￥{totalFishingMinutes * minuteWage:F2}）\n" +
            $"复制 {totalCopy} · 粘贴 {totalPaste} · 手势 {totalGesture}\n" +
            $"少点 {totalSaved} 次 · 休息提醒 {totalReminders} 次\n" +
            "仅本地统计，不上传，不包含剪贴板正文。";
    }

    private static decimal EstimateWorkdayMinutes(WorkstationSettings settings)
    {
        if (settings.WorkEndTime <= settings.WorkStartTime)
        {
            return 8 * 60m;
        }

        var gross = (decimal)(settings.WorkEndTime - settings.WorkStartTime).TotalMinutes;
        if (settings.LunchEndTime > settings.LunchStartTime)
        {
            gross -= (decimal)(settings.LunchEndTime - settings.LunchStartTime).TotalMinutes;
        }

        return Math.Max(0m, gross);
    }

    private bool IsEnabled()
    {
        return _settingsService.Get(SettingKeys.WorkstationEnabled, true) &&
               _settingsService.Get(SettingKeys.EnableWorkBearHub, true);
    }

    private bool IsSprintActive(TimeSpan untilOffWork, WorkTimeStage stage)
    {
        if (!_settingsService.Get(SettingKeys.EnableWorkSprintMode, true))
        {
            return false;
        }

        return _settingsService.Get(SettingKeys.WorkBearSprintManualEnabled, false) ||
               stage == WorkTimeStage.Overtime ||
               (untilOffWork > TimeSpan.Zero && untilOffWork <= TimeSpan.FromMinutes(30));
    }

    private WorkBearTextStyle GetTextStyle()
    {
        var text = _settingsService.Get(SettingKeys.WorkBearTextStyle,
            _settingsService.Get(SettingKeys.WorkstationCopywritingStyle, "打工人模式"));
        return WorkBearTextProvider.ParseStyle(text);
    }

    private WorkstationSettings GetSettings()
    {
        return new WorkstationSettings(
            Math.Max(0, _settingsService.Get(SettingKeys.WorkstationMonthlySalary, 0m)),
            ParseTime(_settingsService.Get(SettingKeys.WorkstationWorkStartTime, "09:00"), new TimeOnly(9, 0)),
            ParseTime(_settingsService.Get(SettingKeys.WorkstationWorkEndTime, "18:00"), new TimeOnly(18, 0)),
            ParseTime(_settingsService.Get(SettingKeys.WorkstationLunchStartTime, "12:00"), new TimeOnly(12, 0)),
            ParseTime(_settingsService.Get(SettingKeys.WorkstationLunchEndTime, "13:00"), new TimeOnly(13, 0)),
            ParseWorkdays(_settingsService.Get(SettingKeys.WorkstationWorkdays, "1,2,3,4,5")),
            Math.Clamp(_settingsService.Get(SettingKeys.WorkstationPayday, 15), 1, 28));
    }

    private DateTimeOffset? GetNextRestReminderAt(DateTimeOffset now)
    {
        if (!IsRestReminderEnabledForToday(now))
        {
            return null;
        }

        var snoozedUntil = _settingsService.Get(SettingKeys.WorkBearRestReminderSnoozedUntil, string.Empty);
        if (DateTimeOffset.TryParse(snoozedUntil, out var until) && until > now)
        {
            return until;
        }

        var interval = Math.Clamp(_settingsService.Get(SettingKeys.WorkstationOverworkReminderIntervalMinutes, 60), 30, 180);
        return now.AddMinutes(interval);
    }

    private bool IsRestReminderEnabledForToday(DateTimeOffset now)
    {
        if (!_settingsService.Get(SettingKeys.WorkstationEnableOverworkReminder, true))
        {
            return false;
        }

        var mutedDate = _settingsService.Get(SettingKeys.WorkBearRestReminderMutedDate, string.Empty);
        return !DateOnly.TryParse(mutedDate, out var date) || date != DateOnly.FromDateTime(now.Date);
    }

    private static int GetCurrentFishingMinutes(WorkstationDailyStats stats, DateTimeOffset now)
    {
        return stats.FishingStartedAt is null
            ? 0
            : Math.Max(0, (int)Math.Floor((now - stats.FishingStartedAt.Value).TotalMinutes));
    }

    private static TimeOnly ParseTime(string value, TimeOnly fallback)
    {
        return TimeOnly.TryParse(value, out var parsed) ? parsed : fallback;
    }

    private static IReadOnlySet<DayOfWeek> ParseWorkdays(string value)
    {
        var days = value
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(part => int.TryParse(part, out var day) ? day : -1)
            .Where(day => day is >= 0 and <= 6)
            .Select(day => (DayOfWeek)day)
            .ToHashSet();

        return days.Count == 0
            ? new HashSet<DayOfWeek>
            {
                DayOfWeek.Monday,
                DayOfWeek.Tuesday,
                DayOfWeek.Wednesday,
                DayOfWeek.Thursday,
                DayOfWeek.Friday
            }
            : days;
    }

    private static decimal GetMinuteWage(WorkstationSettings settings)
    {
        if (settings.MonthlySalary <= 0)
        {
            return 0m;
        }

        var dailySalary = settings.MonthlySalary / WorkdaysPerMonth;
        var workMinutes = Math.Max(1, GetDailyWorkMinutes(settings));
        return dailySalary / workMinutes;
    }

    private static int GetDailyWorkMinutes(WorkstationSettings settings)
    {
        if (settings.WorkEndTime <= settings.WorkStartTime)
        {
            return 1;
        }

        var total = (int)(settings.WorkEndTime - settings.WorkStartTime).TotalMinutes;
        var lunch = settings.LunchEndTime > settings.LunchStartTime
            ? Math.Max(0, (int)(settings.LunchEndTime - settings.LunchStartTime).TotalMinutes)
            : 0;
        return Math.Max(1, total - lunch);
    }

    private static decimal GetMonthEarned(DateTimeOffset now, WorkstationSettings settings)
    {
        var elapsedWorkdays = CountWorkdaysBeforeOrOn(now.Date, settings.Workdays);
        return elapsedWorkdays * (settings.MonthlySalary / WorkdaysPerMonth);
    }

    private static int CountWorkdaysBeforeOrOn(DateTime date, IReadOnlySet<DayOfWeek> workdays)
    {
        var count = 0;
        for (var day = new DateTime(date.Year, date.Month, 1); day <= date; day = day.AddDays(1))
        {
            if (workdays.Contains(day.DayOfWeek))
            {
                count++;
            }
        }

        return count;
    }

    private static decimal GetEffectiveWorkedMinutes(DateTimeOffset now, WorkstationSettings settings)
    {
        if (!settings.Workdays.Contains(now.DayOfWeek) || settings.WorkEndTime <= settings.WorkStartTime)
        {
            return 0;
        }

        var current = TimeOnly.FromDateTime(now.DateTime);
        if (current <= settings.WorkStartTime)
        {
            return 0;
        }

        var end = current < settings.WorkEndTime ? current : settings.WorkEndTime;
        var minutes = Math.Max(0, (decimal)(end - settings.WorkStartTime).TotalMinutes);
        if (settings.LunchEndTime > settings.LunchStartTime)
        {
            var lunchOverlapStart = Max(settings.WorkStartTime, settings.LunchStartTime);
            var lunchOverlapEnd = Min(end, settings.LunchEndTime);
            if (lunchOverlapEnd > lunchOverlapStart)
            {
                minutes -= (decimal)(lunchOverlapEnd - lunchOverlapStart).TotalMinutes;
            }
        }

        return Math.Max(0, minutes);
    }

    private static TimeSpan GetGrossWorkDuration(DateTimeOffset now, WorkstationSettings settings)
    {
        if (!settings.Workdays.Contains(now.DayOfWeek) || settings.WorkEndTime <= settings.WorkStartTime)
        {
            return TimeSpan.Zero;
        }

        var current = TimeOnly.FromDateTime(now.DateTime);
        if (current <= settings.WorkStartTime)
        {
            return TimeSpan.Zero;
        }

        var start = now.Date + settings.WorkStartTime.ToTimeSpan();
        var end = now.Date + (current < settings.WorkEndTime ? current : settings.WorkEndTime).ToTimeSpan();
        return end > start ? end - start : TimeSpan.Zero;
    }

    private static TimeSpan GetOvertimeDuration(DateTimeOffset now, WorkstationSettings settings)
    {
        if (!settings.Workdays.Contains(now.DayOfWeek) || settings.WorkEndTime <= settings.WorkStartTime)
        {
            return TimeSpan.Zero;
        }

        var offWork = now.Date + settings.WorkEndTime.ToTimeSpan();
        return now.DateTime > offWork ? now.DateTime - offWork : TimeSpan.Zero;
    }

    private static TimeSpan GetTimeUntilOffWork(DateTimeOffset now, WorkstationSettings settings)
    {
        if (!settings.Workdays.Contains(now.DayOfWeek) || settings.WorkEndTime <= settings.WorkStartTime)
        {
            return TimeSpan.Zero;
        }

        var offWork = now.Date + settings.WorkEndTime.ToTimeSpan();
        var remaining = offWork - now.DateTime;
        return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
    }

    private static int GetDaysUntilPayday(DateTimeOffset now, int payday)
    {
        var current = now.Date;
        var payDate = new DateTime(current.Year, current.Month, Math.Min(payday, DateTime.DaysInMonth(current.Year, current.Month)));
        if (payDate.Date < current.Date)
        {
            var nextMonth = current.AddMonths(1);
            payDate = new DateTime(nextMonth.Year, nextMonth.Month, Math.Min(payday, DateTime.DaysInMonth(nextMonth.Year, nextMonth.Month)));
        }

        return Math.Max(0, (payDate.Date - current.Date).Days);
    }

    private static WorkBearDailyReport BuildDailyReport(
        DateOnly date,
        TimeSpan workDuration,
        TimeSpan effectiveWorkDuration,
        decimal todayEarned,
        TimeSpan fishingDuration,
        decimal fishingValue,
        WorkstationDailyStats stats,
        TimeSpan overtime,
        string rating,
        string bearLine)
    {
        var text = new StringBuilder();
        text.AppendLine("今日牛马生存报告");
        text.AppendLine();
        text.AppendLine("⏱ 工时");
        text.AppendLine(CultureInfo.InvariantCulture, $"· 坚守工位 {FormatDuration(workDuration)}");
        text.AppendLine(CultureInfo.InvariantCulture, $"· 有效工时约 {FormatDuration(effectiveWorkDuration)} · 加班 {FormatDuration(overtime)}");
        text.AppendLine();
        text.AppendLine("💰 收益（本地估算）");
        text.AppendLine(CultureInfo.InvariantCulture, $"· 老板今日已支出 {FormatMoney(todayEarned)}");
        text.AppendLine();
        text.AppendLine("📊 动作");
        text.AppendLine(CultureInfo.InvariantCulture, $"· 复制 {stats.CopyCount} · 粘贴 {stats.PasteCount} · 手势 {stats.GestureCount} · 打开剪贴板 {stats.OpenClipboardCount}");
        text.AppendLine(CultureInfo.InvariantCulture, $"· 大约少点了 {stats.EstimatedSavedClicks} 次鼠标");
        text.AppendLine();
        text.AppendLine("🐟 摸鱼");
        text.AppendLine(CultureInfo.InvariantCulture, $"· 今日摸鱼 {FormatDuration(fishingDuration)} · 约值 {FormatMoney(fishingValue)}");
        text.AppendLine(CultureInfo.InvariantCulture, $"· 休息提醒 {stats.OverworkReminderCount} 次");
        text.AppendLine();
        text.AppendLine("🐻 小熊点评");
        text.AppendLine(CultureInfo.InvariantCulture, $"· 评级：{rating}");
        text.AppendLine(CultureInfo.InvariantCulture, $"· {bearLine}");
        text.AppendLine();
        text.Append("隐私：报告不含剪贴板正文、图片、浏览器内容或敏感路径。");

        return new WorkBearDailyReport(
            date,
            workDuration,
            effectiveWorkDuration,
            todayEarned,
            fishingDuration,
            fishingValue,
            stats.CopyCount,
            stats.PasteCount,
            stats.GestureCount,
            stats.EstimatedSavedClicks,
            stats.OpenClipboardCount,
            stats.OverworkReminderCount,
            overtime,
            rating,
            bearLine,
            text.ToString());
    }


    private static string MapRestRiskLevel(string restRiskText) => restRiskText switch
    {
        "已超时工作" => "critical",
        "建议活动" => "high",
        "注意休息" => "caution",
        _ => "normal"
    };

    private static string FormatMoney(decimal value) => string.Create(CultureInfo.InvariantCulture, $"￥{value:0.00}");

    private static string FormatDuration(TimeSpan value)
    {
        if (value <= TimeSpan.Zero)
        {
            return "0 分钟";
        }

        return value.TotalHours >= 1
            ? $"{(int)value.TotalHours} 小时 {value.Minutes} 分钟"
            : $"{Math.Max(0, value.Minutes)} 分钟";
    }

    private static TimeOnly Max(TimeOnly first, TimeOnly second) => first > second ? first : second;

    private static TimeOnly Min(TimeOnly first, TimeOnly second) => first < second ? first : second;

    private sealed record WorkstationSettings(
        decimal MonthlySalary,
        TimeOnly WorkStartTime,
        TimeOnly WorkEndTime,
        TimeOnly LunchStartTime,
        TimeOnly LunchEndTime,
        IReadOnlySet<DayOfWeek> Workdays,
        int Payday);
}
