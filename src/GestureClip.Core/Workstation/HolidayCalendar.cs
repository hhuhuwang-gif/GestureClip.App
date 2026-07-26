namespace GestureClip.Core.Workstation;

public sealed record UpcomingBreak(string Name, DateOnly Date, int DaysAway);

/// <summary>
/// 本地内置的中国法定节假日首日表（娱乐倒计时用，不含调休补班）。
/// 数据到期后 GetUpcomingHolidays 返回空列表，UI 自行隐藏。
/// </summary>
public static class HolidayCalendar
{
    private static readonly (string Name, DateOnly Date)[] Holidays =
    [
        ("元旦", new DateOnly(2026, 1, 1)),
        ("春节", new DateOnly(2026, 2, 17)),
        ("清明", new DateOnly(2026, 4, 5)),
        ("五一", new DateOnly(2026, 5, 1)),
        ("端午", new DateOnly(2026, 6, 19)),
        ("中秋", new DateOnly(2026, 9, 25)),
        ("国庆", new DateOnly(2026, 10, 1)),
        ("元旦", new DateOnly(2027, 1, 1)),
        ("春节", new DateOnly(2027, 2, 6)),
        ("清明", new DateOnly(2027, 4, 5)),
        ("五一", new DateOnly(2027, 5, 1)),
        ("端午", new DateOnly(2027, 6, 9)),
        ("中秋", new DateOnly(2027, 9, 15)),
        ("国庆", new DateOnly(2027, 10, 1)),
    ];

    /// <summary>距最近的周六；周六/周日返回 DaysAway = 0（周末进行中）。</summary>
    public static UpcomingBreak GetWeekendCountdown(DateOnly today)
    {
        var days = today.DayOfWeek switch
        {
            DayOfWeek.Saturday or DayOfWeek.Sunday => 0,
            _ => DayOfWeek.Saturday - today.DayOfWeek
        };
        return new UpcomingBreak("周末", today.AddDays(days), days);
    }

    public static IReadOnlyList<UpcomingBreak> GetUpcomingHolidays(DateOnly today, int count = 2)
    {
        return Holidays
            .Where(h => h.Date >= today)
            .OrderBy(h => h.Date)
            .Take(count)
            .Select(h => new UpcomingBreak(h.Name, h.Date, h.Date.DayNumber - today.DayNumber))
            .ToList();
    }
}
