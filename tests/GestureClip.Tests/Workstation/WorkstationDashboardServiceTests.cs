using GestureClip.Core.Abstractions;
using GestureClip.Core.Workstation;
using GestureClip.Features.Workstation;
using Xunit;

namespace GestureClip.Tests.Workstation;

public sealed class WorkstationDashboardServiceTests
{
    [Fact]
    public async Task GetSnapshotAsync_calculates_salary_countdown_and_efficiency()
    {
        var repository = new FakeWorkstationStatsRepository();
        var service = CreateService(repository);
        var now = new DateTimeOffset(2026, 7, 6, 10, 30, 0, TimeSpan.FromHours(8));
        await repository.SaveAsync(new WorkstationDailyStats(DateOnly.FromDateTime(now.Date), 2, 3, 4, 12, 15, null), CancellationToken.None);

        var snapshot = await service.GetSnapshotAsync(now, CancellationToken.None);

        Assert.Equal("工位小熊", snapshot.Title);
        Assert.Equal("坐在你电脑里的打工人状态 Hub", snapshot.Subtitle);
        Assert.Equal(TimeSpan.FromHours(7.5), snapshot.TimeUntilOffWork);
        Assert.Equal(187.50m, decimal.Round(snapshot.TodayEarned, 2));
        Assert.Equal(4000.00m, decimal.Round(snapshot.MonthEarned, 2));
        Assert.Equal(9, snapshot.DaysUntilPayday);
        Assert.Equal(31.25m, decimal.Round(snapshot.TodayFishingValue, 2));
        Assert.Equal(2, snapshot.CopyCount);
        Assert.Equal(3, snapshot.PasteCount);
        Assert.Equal(4, snapshot.GestureCount);
        Assert.Equal(12, snapshot.EstimatedSavedClicks);
        Assert.Equal("开工状态", snapshot.WorkStatusText);
        Assert.Equal("🌅 上午开工", snapshot.WorkStageText);
        Assert.Equal("🌻 晨间小熊", snapshot.BearStatusText);
        Assert.DoesNotContain("secret clipboard text", snapshot.DailyReportText);
    }

    [Fact]
    public async Task GetSnapshotAsync_respects_configured_workdays()
    {
        var repository = new FakeWorkstationStatsRepository();
        var service = new WorkstationDashboardService(new FakeSettingsService { Workdays = "2,3,4" }, repository);
        var monday = new DateTimeOffset(2026, 7, 6, 10, 30, 0, TimeSpan.FromHours(8));

        var snapshot = await service.GetSnapshotAsync(monday, CancellationToken.None);

        Assert.Equal(0m, snapshot.TodayEarned);
        Assert.Equal(2000.00m, decimal.Round(snapshot.MonthEarned, 2));
    }

    [Fact]
    public async Task Fishing_timer_records_current_and_total_value()
    {
        var repository = new FakeWorkstationStatsRepository();
        var service = CreateService(repository);
        var date = new DateOnly(2026, 7, 6);

        await service.StartFishingAsync(new DateTimeOffset(2026, 7, 6, 15, 0, 0, TimeSpan.FromHours(8)), CancellationToken.None);
        var running = await service.GetSnapshotAsync(new DateTimeOffset(2026, 7, 6, 15, 12, 0, TimeSpan.FromHours(8)), CancellationToken.None);
        await service.EndFishingAsync(new DateTimeOffset(2026, 7, 6, 15, 20, 0, TimeSpan.FromHours(8)), CancellationToken.None);
        var ended = await service.GetSnapshotAsync(new DateTimeOffset(2026, 7, 6, 15, 25, 0, TimeSpan.FromHours(8)), CancellationToken.None);

        Assert.True(running.IsFishing);
        Assert.Equal(TimeSpan.FromMinutes(12), running.CurrentFishingDuration);
        Assert.Equal(25.00m, decimal.Round(running.CurrentFishingValue, 2));
        Assert.False(ended.IsFishing);
        Assert.Equal(date, repository.Current.Date);
        Assert.Equal(20, repository.Current.FishingMinutes);
        Assert.Equal(41.67m, decimal.Round(ended.TodayFishingValue, 2));
    }

    [Fact]
    public async Task Recording_events_updates_daily_counts()
    {
        var repository = new FakeWorkstationStatsRepository();
        var service = CreateService(repository);
        var now = new DateTimeOffset(2026, 7, 6, 9, 30, 0, TimeSpan.FromHours(8));

        await service.RecordCopyAsync(now, CancellationToken.None);
        await service.RecordPasteAsync(now, CancellationToken.None);
        await service.RecordGestureAsync(now, CancellationToken.None);
        var snapshot = await service.GetSnapshotAsync(now, CancellationToken.None);

        Assert.Equal(1, snapshot.CopyCount);
        Assert.Equal(1, snapshot.PasteCount);
        Assert.Equal(1, snapshot.GestureCount);
        Assert.Equal(3, snapshot.EstimatedSavedClicks);
    }

    [Fact]
    public async Task Hub_snapshot_contains_report_rest_sprint_and_privacy_fields()
    {
        var repository = new FakeWorkstationStatsRepository();
        var service = CreateService(repository);
        var now = new DateTimeOffset(2026, 7, 6, 17, 40, 0, TimeSpan.FromHours(8));
        await repository.SaveAsync(new WorkstationDailyStats(DateOnly.FromDateTime(now.Date), 20, 18, 12, 120, 35, null, 3, 2), CancellationToken.None);

        var snapshot = await service.GetSnapshotAsync(now, CancellationToken.None);

        Assert.True(snapshot.SprintActive);
        Assert.Equal("🏔 即将下班", snapshot.WorkStageText);
        Assert.Equal("🏁 收尾小熊", snapshot.BearStatusText);
        Assert.Equal("建议活动", snapshot.RestRiskText);
        Assert.Equal(3, snapshot.OpenClipboardCount);
        Assert.Equal(2, snapshot.RestReminderCount);
        Assert.Contains("隐私", snapshot.DailyReportText, StringComparison.Ordinal);
        Assert.DoesNotContain("剪贴板正文：", snapshot.DailyReportText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GenerateDailyReport_excludes_clipboard_body_and_browser_content()
    {
        var repository = new FakeWorkstationStatsRepository();
        var service = CreateService(repository);
        var now = new DateTimeOffset(2026, 7, 6, 18, 20, 0, TimeSpan.FromHours(8));
        await repository.SaveAsync(new WorkstationDailyStats(DateOnly.FromDateTime(now.Date), 2, 3, 4, 12, 15, null), CancellationToken.None);

        var report = await service.GenerateDailyReportAsync(now, CancellationToken.None);

        Assert.Contains("今日牛马生存报告", report.ReportText, StringComparison.Ordinal);
        Assert.Contains("不包含剪贴板正文", report.ReportText, StringComparison.Ordinal);
        Assert.DoesNotContain("password", report.ReportText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("https://", report.ReportText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Recording_events_is_noop_when_workstation_dashboard_is_disabled()
    {
        var repository = new FakeWorkstationStatsRepository();
        var service = new WorkstationDashboardService(new FakeSettingsService { Enabled = false }, repository);
        var now = new DateTimeOffset(2026, 7, 6, 9, 30, 0, TimeSpan.FromHours(8));

        await service.RecordCopyAsync(now, CancellationToken.None);
        await service.RecordPasteAsync(now, CancellationToken.None);
        await service.RecordGestureAsync(now, CancellationToken.None);
        var snapshot = await service.GetSnapshotAsync(now, CancellationToken.None);

        Assert.Equal(0, snapshot.CopyCount);
        Assert.Equal(0, snapshot.PasteCount);
        Assert.Equal(0, snapshot.GestureCount);
        Assert.Equal(0, snapshot.EstimatedSavedClicks);
    }

    [Fact]
    public async Task ResetTodayAsync_clears_daily_stats()
    {
        var repository = new FakeWorkstationStatsRepository();
        var service = CreateService(repository);
        var now = new DateTimeOffset(2026, 7, 6, 9, 30, 0, TimeSpan.FromHours(8));
        await service.RecordCopyAsync(now, CancellationToken.None);
        await service.RecordPasteAsync(now, CancellationToken.None);

        await service.ResetTodayAsync(DateOnly.FromDateTime(now.Date), CancellationToken.None);
        var snapshot = await service.GetSnapshotAsync(now, CancellationToken.None);

        Assert.Equal(0, snapshot.CopyCount);
        Assert.Equal(0, snapshot.PasteCount);
        Assert.Equal(0, snapshot.GestureCount);
    }

    private static WorkstationDashboardService CreateService(FakeWorkstationStatsRepository repository)
    {
        return new WorkstationDashboardService(new FakeSettingsService(), repository);
    }

    private sealed class FakeSettingsService : ISettingsService
    {
        public bool Enabled { get; init; } = true;
        public string Workdays { get; init; } = "1,2,3,4,5";

        public T Get<T>(string key, T defaultValue)
        {
            object value = key switch
            {
                "Workstation.MonthlySalary" => 22000m,
                "Workstation.WorkStartTime" => "09:00",
                "Workstation.WorkEndTime" => "18:00",
                "Workstation.LunchStartTime" => "12:00",
                "Workstation.LunchEndTime" => "13:00",
                "Workstation.Payday" => 15,
                "Workstation.Workdays" => Workdays,
                "Workstation.ShowFishingValue" => true,
                "Workstation.ShowOffWorkCountdown" => true,
                "Workstation.DailyReportEnabled" => false,
                "Workstation.Enabled" => Enabled,
                _ => defaultValue!
            };

            return (T)value;
        }

        public Task SetAsync<T>(string key, T value, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakeWorkstationStatsRepository : IWorkstationStatsRepository
    {
        public WorkstationDailyStats Current { get; private set; } = new(DateOnly.MinValue);

        public Task<WorkstationDailyStats> GetOrCreateAsync(DateOnly date, CancellationToken cancellationToken)
        {
            if (Current.Date != date)
            {
                Current = new WorkstationDailyStats(date);
            }

            return Task.FromResult(Current);
        }

        public Task SaveAsync(WorkstationDailyStats stats, CancellationToken cancellationToken)
        {
            Current = stats;
            return Task.CompletedTask;
        }

        public Task IncrementCountersAsync(
            DateOnly date,
            int copyDelta,
            int pasteDelta,
            int gestureDelta,
            int savedClicksDelta,
            CancellationToken cancellationToken)
        {
            if (Current.Date != date)
            {
                Current = new WorkstationDailyStats(date);
            }

            Current = Current with
            {
                CopyCount = Current.CopyCount + copyDelta,
                PasteCount = Current.PasteCount + pasteDelta,
                GestureCount = Current.GestureCount + gestureDelta,
                EstimatedSavedClicks = Current.EstimatedSavedClicks + savedClicksDelta
            };
            return Task.CompletedTask;
        }

        public Task IncrementHubCountersAsync(
            DateOnly date,
            int openClipboardDelta,
            int overworkReminderDelta,
            CancellationToken cancellationToken)
        {
            if (Current.Date != date)
            {
                Current = new WorkstationDailyStats(date);
            }

            Current = Current with
            {
                OpenClipboardCount = Current.OpenClipboardCount + openClipboardDelta,
                OverworkReminderCount = Current.OverworkReminderCount + overworkReminderDelta
            };
            return Task.CompletedTask;
        }

        public Task ResetAsync(DateOnly date, CancellationToken cancellationToken)
        {
            Current = new WorkstationDailyStats(date);
            return Task.CompletedTask;
        }
    }
}
