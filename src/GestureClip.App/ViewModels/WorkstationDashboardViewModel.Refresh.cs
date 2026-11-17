using GestureClip.Core.Workstation;

namespace GestureClip.App.ViewModels;

public sealed partial class WorkstationDashboardViewModel
{
    private const int PreviousTrendDays = 6;
    private const double MaximumTrendBarHeight = 44;
    private const double MinimumTrendBarHeight = 3;
    private static readonly string[] WeekdayNames = ["日", "一", "二", "三", "四", "五", "六"];
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private IReadOnlyList<TrendCounts> _trendCounts = [];
    private DateOnly? _trendDate;

    public async Task RefreshAsync()
    {
        // Queue each request instead of dropping one after a settings or counter change.
        await _refreshLock.WaitAsync();
        try
        {
            _snapshot = await _dashboardService.GetSnapshotAsync(_timeProvider.GetLocalNow(), CancellationToken.None);
            _snapshotAt = _timeProvider.GetLocalNow();
            RaiseSnapshotProperties();
            await RefreshWeeklyTrendAsync();
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    public sealed record WeeklyTrendDay(string DayLabel, int ActionCount, double BarHeight, bool IsToday, string TooltipText);

    public IReadOnlyList<WeeklyTrendDay> WeeklyTrend { get; private set; } = [];

    private async Task RefreshWeeklyTrendAsync()
    {
        if (_statsRepository is null) return;
        var today = DateOnly.FromDateTime(_timeProvider.GetLocalNow().Date);
        var days = await _statsRepository.GetRangeAsync(today.AddDays(-PreviousTrendDays), today, CancellationToken.None);
        if (_trendDate == today && HasSameTrendCounts(days)) return;

        var trend = CreateWeeklyTrend(days, today);
        _trendCounts = days.Select(day => new TrendCounts(day.Date, day.CopyCount, day.PasteCount, day.GestureCount)).ToArray();
        _trendDate = today;
        WeeklyTrend = Array.AsReadOnly(trend);
        OnPropertyChanged(nameof(WeeklyTrend));
    }

    private bool HasSameTrendCounts(IReadOnlyList<WorkstationDailyStats> days)
    {
        if (days.Count != _trendCounts.Count) return false;
        for (var index = 0; index < days.Count; index++)
        {
            var previous = _trendCounts[index];
            var current = days[index];
            if (previous.Date != current.Date || previous.Copy != current.CopyCount
                || previous.Paste != current.PasteCount || previous.Gesture != current.GestureCount) return false;
        }
        return true;
    }

    private static WeeklyTrendDay[] CreateWeeklyTrend(IReadOnlyList<WorkstationDailyStats> days, DateOnly today)
    {
        var maximum = 1;
        foreach (var day in days) maximum = Math.Max(maximum, ActionCount(day));
        var trend = new WeeklyTrendDay[days.Count];
        for (var index = 0; index < days.Count; index++)
        {
            var day = days[index];
            var total = ActionCount(day);
            trend[index] = new WeeklyTrendDay(WeekdayNames[(int)day.Date.DayOfWeek], total,
                Math.Max(MinimumTrendBarHeight, MaximumTrendBarHeight * total / maximum), day.Date == today,
                $"{day.Date:M月d日} · 动作 {total}（复制 {day.CopyCount} / 粘贴 {day.PasteCount} / 手势 {day.GestureCount}）");
        }
        return trend;
    }

    private static int ActionCount(WorkstationDailyStats day) => day.CopyCount + day.PasteCount + day.GestureCount;

    private readonly record struct TrendCounts(DateOnly Date, int Copy, int Paste, int Gesture);
}
