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

    public WorkstationDashboardService(
        ISettingsService settingsService,
        IWorkstationStatsRepository statsRepository)
    {
        _settingsService = settingsService;
        _statsRepository = statsRepository;
    }

    public async Task<WorkstationDashboardSnapshot> GetSnapshotAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var settings = GetSettings();
        var date = DateOnly.FromDateTime(now.Date);
        var stats = await _statsRepository.GetOrCreateAsync(date, cancellationToken);
        var minuteWage = GetMinuteWage(settings);
        var earnedMinutes = GetEffectiveWorkedMinutes(now, settings);
        var currentFishingMinutes = stats.FishingStartedAt is null
            ? 0
            : Math.Max(0, (int)Math.Floor((now - stats.FishingStartedAt.Value).TotalMinutes));
        var totalFishingMinutes = stats.FishingMinutes + currentFishingMinutes;

        return new WorkstationDashboardSnapshot(
            "工位小熊",
            "今天也在低功耗运行",
            GetTimeUntilOffWork(now, settings),
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
            GetWorkStatusText(now));
    }

    public async Task StartFishingAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (!IsEnabled())
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

        var minutes = Math.Max(0, (int)Math.Floor((now - stats.FishingStartedAt.Value).TotalMinutes));
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

    public async Task RecordCopyAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (!IsEnabled())
        {
            return;
        }

        await _statsRepository.IncrementCountersAsync(
            DateOnly.FromDateTime(now.Date),
            copyDelta: 1,
            pasteDelta: 0,
            gestureDelta: 0,
            savedClicksDelta: 0,
            cancellationToken);
    }

    public async Task RecordPasteAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (!IsEnabled())
        {
            return;
        }

        await _statsRepository.IncrementCountersAsync(
            DateOnly.FromDateTime(now.Date),
            copyDelta: 0,
            pasteDelta: 1,
            gestureDelta: 0,
            savedClicksDelta: 0,
            cancellationToken);
    }

    public async Task RecordGestureAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (!IsEnabled())
        {
            return;
        }

        await _statsRepository.IncrementCountersAsync(
            DateOnly.FromDateTime(now.Date),
            copyDelta: 0,
            pasteDelta: 0,
            gestureDelta: 1,
            savedClicksDelta: SavedClicksPerGesture,
            cancellationToken);
    }

    private bool IsEnabled()
    {
        return _settingsService.Get(SettingKeys.WorkstationEnabled, true);
    }

    private WorkstationSettings GetSettings()
    {
        return new WorkstationSettings(
            _settingsService.Get(SettingKeys.WorkstationMonthlySalary, 0m),
            ParseTime(_settingsService.Get(SettingKeys.WorkstationWorkStartTime, "09:00"), new TimeOnly(9, 0)),
            ParseTime(_settingsService.Get(SettingKeys.WorkstationWorkEndTime, "18:00"), new TimeOnly(18, 0)),
            ParseTime(_settingsService.Get(SettingKeys.WorkstationLunchStartTime, "12:00"), new TimeOnly(12, 0)),
            ParseTime(_settingsService.Get(SettingKeys.WorkstationLunchEndTime, "13:00"), new TimeOnly(13, 0)),
            ParseWorkdays(_settingsService.Get(SettingKeys.WorkstationWorkdays, "1,2,3,4,5")),
            Math.Clamp(_settingsService.Get(SettingKeys.WorkstationPayday, 15), 1, 28));
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
        var dailySalary = settings.MonthlySalary / WorkdaysPerMonth;
        var workMinutes = Math.Max(1, GetDailyWorkMinutes(settings));
        return dailySalary / workMinutes;
    }

    private static int GetDailyWorkMinutes(WorkstationSettings settings)
    {
        var total = (int)(settings.WorkEndTime - settings.WorkStartTime).TotalMinutes;
        var lunch = Math.Max(0, (int)(settings.LunchEndTime - settings.LunchStartTime).TotalMinutes);
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
        if (!settings.Workdays.Contains(now.DayOfWeek))
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
        var lunchOverlapStart = Max(settings.WorkStartTime, settings.LunchStartTime);
        var lunchOverlapEnd = Min(end, settings.LunchEndTime);
        if (lunchOverlapEnd > lunchOverlapStart)
        {
            minutes -= (decimal)(lunchOverlapEnd - lunchOverlapStart).TotalMinutes;
        }

        return Math.Max(0, minutes);
    }

    private static TimeSpan GetTimeUntilOffWork(DateTimeOffset now, WorkstationSettings settings)
    {
        if (!settings.Workdays.Contains(now.DayOfWeek))
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

    private static string GetWorkStatusText(DateTimeOffset now)
    {
        var time = TimeOnly.FromDateTime(now.DateTime);
        if (time < new TimeOnly(10, 0)) return "开机缓冲期";
        if (time < new TimeOnly(12, 0)) return "假装高效期";
        if (time < new TimeOnly(14, 0)) return "灵魂离线期";
        if (time < new TimeOnly(17, 30)) return "低功耗运行期";
        if (time < new TimeOnly(18, 0)) return "禁止新增需求期";
        return "非法占用人生时间";
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
