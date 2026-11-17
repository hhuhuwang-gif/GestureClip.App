using GestureClip.App.ViewModels;
using GestureClip.Core.Settings;
using System.Windows.Media;
using Xunit;

namespace GestureClip.Tests.Workstation;

public sealed partial class WorkstationDashboardViewModelTests
{
    [Fact]
    public void Stable_visual_resource_reads_reuse_frozen_brushes_without_allocating()
    {
        var viewModel = new WorkstationDashboardViewModel(new FakeWorkstationDashboardService());
        var brushes = ReadBrushes(viewModel);
        foreach (var brush in brushes) Assert.True(brush.IsFrozen);
        var holidays = viewModel.HolidayCountdownTexts;
        var allocationBefore = GC.GetAllocatedBytesForCurrentThread();
        const int readCount = 1_000;
        for (var index = 0; index < readCount; index++)
        {
            _ = viewModel.StageAccentBrush;
            _ = viewModel.StageStartBrush;
            _ = viewModel.StageSoftBrush;
            _ = viewModel.RestRiskBrush;
            _ = viewModel.RestRiskSoftBrush;
            _ = viewModel.FishingBorderBrush;
            _ = viewModel.HolidayCountdownTexts;
        }
        var allocated = GC.GetAllocatedBytesForCurrentThread() - allocationBefore;
        Assert.Equal(0, allocated);
        Assert.Same(holidays, viewModel.HolidayCountdownTexts);
        var repeatedBrushes = ReadBrushes(viewModel);
        for (var index = 0; index < brushes.Length; index++) Assert.Same(brushes[index], repeatedBrushes[index]);
    }

    [Fact]
    public async Task Visual_resources_change_only_when_their_actual_colors_change()
    {
        var service = new FakeWorkstationDashboardService();
        var viewModel = new WorkstationDashboardViewModel(service);
        await viewModel.RefreshAsync();
        var original = ReadBrushes(viewModel);
        service.Snapshot = service.Snapshot with { CopyCount = 12 };
        await viewModel.RefreshAsync();
        var unchanged = ReadBrushes(viewModel);
        for (var index = 0; index < original.Length; index++) Assert.Same(original[index], unchanged[index]);

        service.Snapshot = service.Snapshot with { StageAccentColor = "#F87171", RestRiskLevel = "high", IsFishing = true };
        await viewModel.RefreshAsync();
        Assert.NotSame(original[0], viewModel.StageAccentBrush);
        Assert.Same(original[1], viewModel.StageStartBrush);
        Assert.NotSame(original[2], viewModel.StageSoftBrush);
        Assert.NotSame(original[3], viewModel.RestRiskBrush);
        Assert.NotSame(original[5], viewModel.FishingBorderBrush);
        Assert.Equal((Color)ColorConverter.ConvertFromString("#F87171")!, ((SolidColorBrush)viewModel.StageAccentBrush).Color);
        Assert.True(viewModel.StageAccentBrush.IsFrozen);
    }

    [Fact]
    public async Task Holiday_countdowns_reuse_the_day_and_update_across_midnight()
    {
        var clock = new ManualTimeProvider(new DateTimeOffset(2026, 11, 16, 10, 0, 0, TimeSpan.Zero));
        var viewModel = new WorkstationDashboardViewModel(new FakeWorkstationDashboardService(), timeProvider: clock);
        var monday = viewModel.HolidayCountdownTexts;
        Assert.Contains("距周末 5 天", monday);
        clock.Now = clock.Now.AddHours(5);
        await viewModel.RefreshAsync();
        Assert.Same(monday, viewModel.HolidayCountdownTexts);
        clock.Now = clock.Now.AddDays(1);
        await viewModel.RefreshAsync();
        Assert.NotSame(monday, viewModel.HolidayCountdownTexts);
        Assert.Contains("距周末 4 天", viewModel.HolidayCountdownTexts);
    }

    [Fact]
    public async Task Equal_weekly_data_preserves_collection_and_item_references()
    {
        var clock = new ManualTimeProvider(new DateTimeOffset(2026, 11, 16, 10, 0, 0, TimeSpan.Zero));
        var repository = new FakeStatsRepository { Days = [new(new DateOnly(2026, 11, 16), CopyCount: 3)] };
        var viewModel = new WorkstationDashboardViewModel(new FakeWorkstationDashboardService(), statsRepository: repository, timeProvider: clock);
        await viewModel.RefreshAsync();
        var first = viewModel.WeeklyTrend;
        var notifications = new List<string?>();
        viewModel.PropertyChanged += (_, e) => notifications.Add(e.PropertyName);
        repository.Days = repository.Days.Select(day => day with { FishingMinutes = 20 }).ToArray();
        await viewModel.RefreshAsync();
        Assert.Equal(2, repository.ReadCount);
        Assert.Same(first, viewModel.WeeklyTrend);
        Assert.Same(first[0], viewModel.WeeklyTrend[0]);
        Assert.DoesNotContain(nameof(viewModel.WeeklyTrend), notifications);
    }

    [Fact]
    public async Task Weekly_data_detects_counter_composition_changes_even_with_same_total()
    {
        var repository = new FakeStatsRepository { Days = [new(new DateOnly(2026, 11, 16), CopyCount: 3)] };
        var viewModel = new WorkstationDashboardViewModel(new FakeWorkstationDashboardService(), statsRepository: repository);
        await viewModel.RefreshAsync();
        var first = viewModel.WeeklyTrend;
        repository.Days = [repository.Days[0] with { CopyCount = 1, PasteCount = 2 }];
        await viewModel.RefreshAsync();
        Assert.NotSame(first, viewModel.WeeklyTrend);
        Assert.Equal(3, viewModel.WeeklyTrend[0].ActionCount);
        Assert.Contains("复制 1 / 粘贴 2", viewModel.WeeklyTrend[0].TooltipText);
    }

    [Fact]
    public async Task Weekly_today_marker_updates_when_the_day_changes()
    {
        var date = new DateOnly(2026, 11, 16);
        var clock = new ManualTimeProvider(new DateTimeOffset(2026, 11, 16, 23, 59, 0, TimeSpan.Zero));
        var repository = new FakeStatsRepository { Days = [new(date), new(date.AddDays(1))] };
        var viewModel = new WorkstationDashboardViewModel(new FakeWorkstationDashboardService(), statsRepository: repository, timeProvider: clock);
        await viewModel.RefreshAsync();
        Assert.True(viewModel.WeeklyTrend[0].IsToday);
        clock.Now = clock.Now.AddMinutes(2);
        await viewModel.RefreshAsync();
        Assert.False(viewModel.WeeklyTrend[0].IsToday);
        Assert.True(viewModel.WeeklyTrend[1].IsToday);
    }

    [Fact]
    public async Task Trend_errors_surface_and_do_not_block_a_later_refresh()
    {
        var repository = new FakeStatsRepository { Error = new InvalidOperationException("range failed") };
        var viewModel = new WorkstationDashboardViewModel(new FakeWorkstationDashboardService(), statsRepository: repository);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => viewModel.RefreshAsync());
        Assert.Equal("range failed", error.Message);
        repository.Error = null;
        await viewModel.RefreshAsync();
        Assert.Equal(2, repository.ReadCount);
        Assert.Empty(viewModel.WeeklyTrend);
    }

    private static Brush[] ReadBrushes(WorkstationDashboardViewModel viewModel) =>
    [
        viewModel.StageAccentBrush, viewModel.StageStartBrush, viewModel.StageSoftBrush,
        viewModel.RestRiskBrush, viewModel.RestRiskSoftBrush, viewModel.FishingBorderBrush
    ];
}
