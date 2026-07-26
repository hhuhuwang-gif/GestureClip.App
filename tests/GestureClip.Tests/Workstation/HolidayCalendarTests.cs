using GestureClip.Core.Workstation;
using Xunit;

namespace GestureClip.Tests.Workstation;

public sealed class HolidayCalendarTests
{
    [Theory]
    [InlineData(2026, 7, 27, 5)]  // 周一 → 距周六 5 天
    [InlineData(2026, 7, 31, 1)]  // 周五 → 1 天
    [InlineData(2026, 8, 1, 0)]   // 周六 → 进行中
    [InlineData(2026, 8, 2, 0)]   // 周日 → 进行中
    public void Weekend_countdown_counts_days_until_saturday(int year, int month, int day, int expected)
    {
        var result = HolidayCalendar.GetWeekendCountdown(new DateOnly(year, month, day));

        Assert.Equal("周末", result.Name);
        Assert.Equal(expected, result.DaysAway);
    }

    [Fact]
    public void Upcoming_holidays_are_ordered_and_counted_from_today()
    {
        var result = HolidayCalendar.GetUpcomingHolidays(new DateOnly(2026, 7, 27));

        Assert.Equal(2, result.Count);
        Assert.Equal("中秋", result[0].Name);
        Assert.Equal(new DateOnly(2026, 9, 25), result[0].Date);
        Assert.Equal(60, result[0].DaysAway);
        Assert.Equal("国庆", result[1].Name);
        Assert.Equal(66, result[1].DaysAway);
    }

    [Fact]
    public void Holiday_on_today_reports_zero_days()
    {
        var result = HolidayCalendar.GetUpcomingHolidays(new DateOnly(2026, 10, 1), count: 1);

        Assert.Equal("国庆", result[0].Name);
        Assert.Equal(0, result[0].DaysAway);
    }

    [Fact]
    public void Upcoming_holidays_cross_year_boundary()
    {
        var result = HolidayCalendar.GetUpcomingHolidays(new DateOnly(2026, 10, 2));

        Assert.Equal("元旦", result[0].Name);
        Assert.Equal(new DateOnly(2027, 1, 1), result[0].Date);
        Assert.Equal("春节", result[1].Name);
    }

    [Fact]
    public void Exhausted_table_returns_empty_instead_of_throwing()
    {
        var result = HolidayCalendar.GetUpcomingHolidays(new DateOnly(2028, 1, 1));

        Assert.Empty(result);
    }
}
