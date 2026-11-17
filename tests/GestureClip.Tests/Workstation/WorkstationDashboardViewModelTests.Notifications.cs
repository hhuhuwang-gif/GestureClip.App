using GestureClip.App.ViewModels;
using GestureClip.Core.Settings;
using GestureClip.Core.Workstation;
using Xunit;

namespace GestureClip.Tests.Workstation;

public sealed partial class WorkstationDashboardViewModelTests
{
    [Fact]
    public async Task Equal_snapshot_refresh_does_not_repeat_binding_notifications()
    {
        var clock = new ManualTimeProvider(new DateTimeOffset(2026, 11, 16, 10, 0, 0, TimeSpan.Zero));
        var viewModel = new WorkstationDashboardViewModel(new FakeWorkstationDashboardService(), timeProvider: clock);
        await viewModel.RefreshAsync();
        var notifications = new List<string?>();
        viewModel.PropertyChanged += (_, e) => notifications.Add(e.PropertyName);
        await viewModel.RefreshAsync();
        Assert.Empty(notifications);
    }

    [Fact]
    public async Task Counter_only_change_notifies_only_affected_bindings()
    {
        var clock = new ManualTimeProvider(new DateTimeOffset(2026, 11, 16, 10, 0, 0, TimeSpan.Zero));
        var service = new FakeWorkstationDashboardService();
        var viewModel = new WorkstationDashboardViewModel(service, timeProvider: clock);
        await viewModel.RefreshAsync();
        var notifications = new List<string?>();
        viewModel.PropertyChanged += (_, e) => notifications.Add(e.PropertyName);
        service.Snapshot = service.Snapshot with { CopyCount = 7 };
        await viewModel.RefreshAsync();
        Assert.Equal([nameof(viewModel.CopyCountText), nameof(viewModel.ActionStatsText)], notifications);
    }

    [Fact]
    public async Task Settings_only_change_notifies_the_actual_bound_properties()
    {
        var settings = new FakeSettingsService();
        var clock = new ManualTimeProvider(new DateTimeOffset(2026, 11, 16, 10, 0, 0, TimeSpan.Zero));
        var service = new FakeWorkstationDashboardService { Snapshot = Snapshot with { CurrentFishingValue = 12m } };
        var viewModel = new WorkstationDashboardViewModel(service, settings, timeProvider: clock);
        await viewModel.RefreshAsync();
        var notifications = new List<string?>();
        viewModel.PropertyChanged += (_, e) => notifications.Add(e.PropertyName);
        settings.Values[SettingKeys.WorkstationShowFishingValue] = false;
        settings.Values[SettingKeys.WorkstationMonthlySalary] = 12000m;
        settings.Values[SettingKeys.WellnessWaterReminderEnabled] = true;
        await viewModel.RefreshAsync();
        Assert.Equal("已隐藏", viewModel.CurrentFishingValueText);
        Assert.Contains(nameof(viewModel.CurrentFishingValueText), notifications);
        Assert.Contains(nameof(viewModel.WorkstationMonthlySalary), notifications);
        Assert.Contains(nameof(viewModel.WaterReminderEnabled), notifications);
        Assert.DoesNotContain(nameof(viewModel.CopyCountText), notifications);
    }

    [Fact]
    public async Task Queued_refresh_keeps_the_request_after_a_change()
    {
        var completion = new TaskCompletionSource<WorkstationDashboardSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
        var clock = new ManualTimeProvider(new DateTimeOffset(2026, 11, 16, 10, 0, 0, TimeSpan.Zero));
        var service = new FakeWorkstationDashboardService();
        service.SnapshotRequest = () => service.RefreshCount == 1
            ? completion.Task : Task.FromResult(Snapshot with { CopyCount = 9 });
        var viewModel = new WorkstationDashboardViewModel(service, timeProvider: clock);
        var first = viewModel.RefreshAsync();
        var afterChange = viewModel.RefreshAsync();
        Assert.Equal(1, service.RefreshCount);
        completion.SetResult(Snapshot with { CopyCount = 1 });
        await Task.WhenAll(first, afterChange);
        Assert.Equal(2, service.RefreshCount);
        Assert.Equal("9", viewModel.CopyCountText);
    }

    [Fact]
    public async Task Snapshot_error_releases_the_refresh_queue_for_retry()
    {
        var service = new FakeWorkstationDashboardService();
        service.SnapshotRequest = () => Task.FromException<WorkstationDashboardSnapshot>(new InvalidOperationException("snapshot failed"));
        var viewModel = new WorkstationDashboardViewModel(service);
        await Assert.ThrowsAsync<InvalidOperationException>(() => viewModel.RefreshAsync());
        service.SnapshotRequest = null;
        await viewModel.RefreshAsync();
        Assert.Equal(2, service.RefreshCount);
    }

    [Fact]
    public void Dashboard_starts_with_summary_visible_and_details_collapsed()
    {
        var viewModel = new WorkstationDashboardViewModel(new FakeWorkstationDashboardService());
        Assert.True(viewModel.EarningsExpanded);
        Assert.False(viewModel.SettingsExpanded);
        Assert.False(viewModel.FishingExpanded);
        Assert.False(viewModel.RestExpanded);
        Assert.False(viewModel.SprintExpanded);
        Assert.False(viewModel.ReportExpanded);
        Assert.False(viewModel.PeriodExpanded);
    }
}
