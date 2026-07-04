using GestureClip.Core.Abstractions;
using GestureClip.Core.Gestures;
using GestureClip.Core.Settings;
using GestureClip.Core.WorkerLevel;
using GestureClip.Core.Workstation;
using GestureClip.Features.Workstation;
using GestureClip.Tests.TestDoubles;
using Xunit;

namespace GestureClip.Tests.Workstation;

public sealed class WorkstationHudServiceTests
{
    [Fact]
    public async Task BuildSnapshotAsync_generates_copy_fun_report()
    {
        var service = CreateService();
        var hudInfo = new GestureHudInfo("↑", "U", "复制", "Ctrl + C", "编辑增强模式")
        {
            Action = BuiltInGestureAction.Copy
        };

        var snapshot = await service.BuildSnapshotAsync(hudInfo, gainedXp: 3, DateTimeOffset.Parse("2026-07-04T10:00:00+08:00"), CancellationToken.None);

        Assert.False(string.IsNullOrWhiteSpace(snapshot.FunText));
        Assert.Equal("经验 +3", snapshot.GainedXpText);
        Assert.Equal("Lv.5 右键小法师", snapshot.LevelText);
        Assert.Equal("XP 128 / 900", snapshot.XpText);
    }

    [Theory]
    [InlineData(BuiltInGestureAction.Paste)]
    [InlineData(BuiltInGestureAction.Enter)]
    [InlineData(BuiltInGestureAction.Escape)]
    public async Task BuildSnapshotAsync_generates_action_specific_fun_report(BuiltInGestureAction action)
    {
        var service = CreateService();
        var hudInfo = new GestureHudInfo("↓", "D", action.ToString(), "Shortcut", "自定义模式")
        {
            Action = action
        };

        var snapshot = await service.BuildSnapshotAsync(hudInfo, gainedXp: 3, DateTimeOffset.Now, CancellationToken.None);

        Assert.False(string.IsNullOrWhiteSpace(snapshot.FunText));
    }

    [Fact]
    public async Task BuildSnapshotAsync_returns_salary_countdown_and_stats_without_clipboard_content()
    {
        var service = CreateService();
        var hudInfo = new GestureHudInfo("↓", "D", "粘贴", "Ctrl + V", "自定义模式")
        {
            Action = BuiltInGestureAction.Paste
        };

        var snapshot = await service.BuildSnapshotAsync(hudInfo, gainedXp: 3, DateTimeOffset.Now, CancellationToken.None);

        Assert.Equal("今日 ￥186.40 · 下班 2小时15分 · 发薪 11 天", snapshot.WorkSummaryText);
        Assert.Equal("手势 37 · 复制 25 · 粘贴 18 · 少点 111 次", snapshot.StatsText);
        Assert.DoesNotContain("secret clipboard text", snapshot.FunText + snapshot.WorkSummaryText + snapshot.StatsText);
    }

    [Fact]
    public async Task BuildSnapshotAsync_estimates_gesture_xp_when_not_provided()
    {
        var service = CreateService();
        var hudInfo = new GestureHudInfo("↓", "D", "粘贴", "Ctrl + V", "自定义模式")
        {
            Action = BuiltInGestureAction.Paste
        };

        var snapshot = await service.BuildSnapshotAsync(hudInfo, gainedXp: 0, DateTimeOffset.Now, CancellationToken.None);

        Assert.Equal("经验 +3", snapshot.GainedXpText);
    }
    [Fact]
    public async Task BuildSnapshotAsync_uses_multiple_local_fun_reports_for_same_action()
    {
        var service = CreateService();
        var hudInfo = new GestureHudInfo("↓", "D", "粘贴", "Ctrl + V", "自定义模式")
        {
            Action = BuiltInGestureAction.Paste
        };

        var reports = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < 12; i++)
        {
            var snapshot = await service.BuildSnapshotAsync(hudInfo, gainedXp: 3, DateTimeOffset.Now.AddSeconds(i), CancellationToken.None);
            reports.Add(snapshot.FunText);
        }

        Assert.True(reports.Count >= 2, string.Join(" | ", reports));
        Assert.All(reports, report => Assert.False(string.IsNullOrWhiteSpace(report)));
    }

    [Fact]
    public async Task BuildSnapshotAsync_reuses_dashboard_and_level_snapshots_for_one_second()
    {
        var settings = new FakeSettingsService();
        settings.Values[SettingKeys.HudFunTextEnabled] = true;
        settings.Values[SettingKeys.HudStatusLevelEnabled] = true;
        settings.Values[SettingKeys.WorkerLevelShowLevelInHud] = true;
        var dashboard = new FakeDashboardService();
        var level = new FakeWorkerLevelService();
        var service = new WorkstationHudService(settings, dashboard, level);
        var hudInfo = new GestureHudInfo("↓", "D", "粘贴", "Ctrl + V", "自定义模式")
        {
            Action = BuiltInGestureAction.Paste
        };
        var now = DateTimeOffset.Parse("2026-07-04T10:00:00+08:00");

        await service.BuildSnapshotAsync(hudInfo, gainedXp: 3, now, CancellationToken.None);
        await service.BuildSnapshotAsync(hudInfo, gainedXp: 3, now.AddMilliseconds(500), CancellationToken.None);

        Assert.Equal(1, dashboard.GetSnapshotCallCount);
        Assert.Equal(1, level.GetSnapshotCallCount);

        await service.BuildSnapshotAsync(hudInfo, gainedXp: 3, now.AddSeconds(2), CancellationToken.None);

        Assert.Equal(2, dashboard.GetSnapshotCallCount);
        Assert.Equal(2, level.GetSnapshotCallCount);
    }

    private static WorkstationHudService CreateService()
    {
        var settings = new FakeSettingsService();
        settings.Values[SettingKeys.HudFunTextEnabled] = true;
        settings.Values[SettingKeys.HudStatusLevelEnabled] = true;
        settings.Values[SettingKeys.WorkerLevelShowLevelInHud] = true;
        return new WorkstationHudService(
            settings,
            new FakeDashboardService(),
            new FakeWorkerLevelService());
    }

    private sealed class FakeDashboardService : IWorkstationDashboardService
    {
        public int GetSnapshotCallCount { get; private set; }

        public Task<WorkstationDashboardSnapshot> GetSnapshotAsync(DateTimeOffset now, CancellationToken cancellationToken)
        {
            GetSnapshotCallCount++;
            return Task.FromResult(new WorkstationDashboardSnapshot(
                "工位小熊",
                "今天也在低功耗运行",
                TimeSpan.FromHours(2) + TimeSpan.FromMinutes(15),
                186.40m,
                3000m,
                11,
                false,
                TimeSpan.Zero,
                0m,
                0m,
                25,
                18,
                37,
                111,
                "低功耗运行期"));
        }

        public Task StartFishingAsync(DateTimeOffset now, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task EndFishingAsync(DateTimeOffset now, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task ResetTodayAsync(DateOnly date, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task RecordCopyAsync(DateTimeOffset now, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task RecordPasteAsync(DateTimeOffset now, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task RecordGestureAsync(DateTimeOffset now, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakeWorkerLevelService : IWorkerLevelService
    {
        public int GetSnapshotCallCount { get; private set; }

        public Task<WorkerLevelSnapshot> GetSnapshotAsync(CancellationToken cancellationToken)
        {
            GetSnapshotCallCount++;
            return Task.FromResult(new WorkerLevelSnapshot(
                128,
                42,
                new WorkerLevelDefinition(5, 500, "右键小法师"),
                new WorkerLevelDefinition(6, 900, "工位老油条"),
                0,
                400,
                0.32,
                false,
                5,
                null));
        }

        public Task<WorkerLevelSnapshot> RecordActionAsync(BuiltInGestureAction action, bool isGestureSuccess, DateTimeOffset now, CancellationToken cancellationToken)
        {
            return GetSnapshotAsync(cancellationToken);
        }
    }
}






