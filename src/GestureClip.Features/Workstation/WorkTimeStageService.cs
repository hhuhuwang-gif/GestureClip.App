using GestureClip.Core.Abstractions;
using GestureClip.Core.Settings;
using GestureClip.Core.Workstation;

namespace GestureClip.Features.Workstation;

public sealed class WorkTimeStageService : IWorkTimeStageService
{
    private readonly ISettingsService _settingsService;

    public WorkTimeStageService(ISettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    public WorkTimeStageSnapshot GetSnapshot(DateTimeOffset now)
    {
        var settings = GetSettings();
        var currentTime = TimeOnly.FromDateTime(now.DateTime);
        if (!settings.Workdays.Contains(now.DayOfWeek))
        {
            return Create(WorkTimeStage.RestDay, 0, TimeSpan.Zero);
        }

        if (settings.WorkEndTime <= settings.WorkStartTime)
        {
            return Create(WorkTimeStage.OffWork, 0, TimeSpan.Zero);
        }

        if (currentTime < settings.WorkStartTime)
        {
            return Create(WorkTimeStage.BeforeWork, 0, TimeSpan.Zero);
        }

        if (IsLunch(currentTime, settings))
        {
            var workedBeforeLunch = settings.LunchStartTime > settings.WorkStartTime
                ? settings.LunchStartTime - settings.WorkStartTime
                : TimeSpan.Zero;
            return Create(WorkTimeStage.LunchBreak, Progress(workedBeforeLunch, settings), workedBeforeLunch);
        }

        if (currentTime >= settings.WorkEndTime)
        {
            var full = EffectiveWorkDuration(settings.WorkEndTime, settings);
            return Create(WorkTimeStage.Overtime, 1, full);
        }

        var worked = EffectiveWorkDuration(currentTime, settings);
        var progress = Progress(worked, settings);
        var stage = progress < 0.33
            ? WorkTimeStage.EarlyWork
            : progress < 0.75
                ? WorkTimeStage.MidWork
                : WorkTimeStage.LateWork;
        return Create(stage, progress, worked);
    }

    private WorkTimeStageSnapshot Create(WorkTimeStage stage, double progress, TimeSpan worked)
    {
        return new WorkTimeStageSnapshot(stage, Math.Clamp(progress, 0, 1), worked, WorkTimeStageThemeProvider.GetTheme(stage));
    }

    private WorkTimeSettings GetSettings()
    {
        return new WorkTimeSettings(
            ParseTime(_settingsService.Get(SettingKeys.WorkstationWorkStartTime, "09:00"), new TimeOnly(9, 0)),
            ParseTime(_settingsService.Get(SettingKeys.WorkstationWorkEndTime, "18:00"), new TimeOnly(18, 0)),
            ParseTime(_settingsService.Get(SettingKeys.WorkstationLunchStartTime, "12:00"), new TimeOnly(12, 0)),
            ParseTime(_settingsService.Get(SettingKeys.WorkstationLunchEndTime, "13:00"), new TimeOnly(13, 0)),
            ParseWorkdays(_settingsService.Get(SettingKeys.WorkstationWorkdays, "1,2,3,4,5")));
    }

    private static bool IsLunch(TimeOnly current, WorkTimeSettings settings)
    {
        return settings.LunchEndTime > settings.LunchStartTime &&
            current >= settings.LunchStartTime &&
            current < settings.LunchEndTime;
    }

    private static TimeSpan EffectiveWorkDuration(TimeOnly current, WorkTimeSettings settings)
    {
        var end = current < settings.WorkEndTime ? current : settings.WorkEndTime;
        if (end <= settings.WorkStartTime)
        {
            return TimeSpan.Zero;
        }

        var worked = end - settings.WorkStartTime;
        if (settings.LunchEndTime > settings.LunchStartTime)
        {
            var overlapStart = Max(settings.WorkStartTime, settings.LunchStartTime);
            var overlapEnd = Min(end, settings.LunchEndTime);
            if (overlapEnd > overlapStart)
            {
                worked -= overlapEnd - overlapStart;
            }
        }

        return worked > TimeSpan.Zero ? worked : TimeSpan.Zero;
    }

    private static double Progress(TimeSpan worked, WorkTimeSettings settings)
    {
        var total = EffectiveWorkDuration(settings.WorkEndTime, settings);
        return total <= TimeSpan.Zero ? 0 : worked.TotalMinutes / total.TotalMinutes;
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

    private static TimeOnly Max(TimeOnly first, TimeOnly second) => first > second ? first : second;

    private static TimeOnly Min(TimeOnly first, TimeOnly second) => first < second ? first : second;

    private sealed record WorkTimeSettings(
        TimeOnly WorkStartTime,
        TimeOnly WorkEndTime,
        TimeOnly LunchStartTime,
        TimeOnly LunchEndTime,
        IReadOnlySet<DayOfWeek> Workdays);
}
