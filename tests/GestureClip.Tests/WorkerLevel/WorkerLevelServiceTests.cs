using GestureClip.Core.Gestures;
using GestureClip.Core.Settings;
using GestureClip.Features.WorkerLevel;
using GestureClip.Tests.TestDoubles;
using Xunit;

namespace GestureClip.Tests.WorkerLevel;

public sealed class WorkerLevelServiceTests
{
    [Fact]
    public async Task GetSnapshotAsync_returns_initial_level()
    {
        var settings = new FakeSettingsService();
        var service = new WorkerLevelService(settings);

        var snapshot = await service.GetSnapshotAsync(CancellationToken.None);

        Assert.Equal(1, snapshot.CurrentLevel.Level);
        Assert.Equal("初入工位", snapshot.CurrentLevel.Title);
        Assert.Equal(0, snapshot.TotalXp);
        Assert.Equal(0, snapshot.TotalActionCount);
    }

    [Theory]
    [InlineData(BuiltInGestureAction.Copy, 1)]
    [InlineData(BuiltInGestureAction.Paste, 1)]
    [InlineData(BuiltInGestureAction.OpenClipboardOverlay, 2)]
    [InlineData(BuiltInGestureAction.PasteLatestClipboardItem, 3)]
    public async Task RecordActionAsync_adds_expected_xp(BuiltInGestureAction action, int expectedXp)
    {
        var settings = new FakeSettingsService();
        var service = new WorkerLevelService(settings);

        var snapshot = await service.RecordActionAsync(action, true, DateTimeOffset.UtcNow, CancellationToken.None);

        Assert.Equal(expectedXp + 2, snapshot.TotalXp);
        Assert.Equal(1, snapshot.TotalActionCount);
    }

    [Fact]
    public async Task RecordActionAsync_levels_up_when_threshold_reached()
    {
        var settings = new FakeSettingsService();
        settings.Values[SettingKeys.WorkerLevelTotalXp] = 49;
        settings.Values[SettingKeys.WorkerLevelCurrentLevel] = 1;
        var service = new WorkerLevelService(settings);

        var snapshot = await service.RecordActionAsync(BuiltInGestureAction.Copy, false, DateTimeOffset.UtcNow, CancellationToken.None);

        Assert.Equal(2, snapshot.CurrentLevel.Level);
        Assert.Equal("复制学徒", snapshot.CurrentLevel.Title);
        Assert.True(snapshot.LeveledUp);
        Assert.Equal(1, snapshot.PreviousLevel);
    }

    [Fact]
    public async Task RecordActionAsync_crosses_multiple_levels()
    {
        var settings = new FakeSettingsService();
        settings.Values[SettingKeys.WorkerLevelTotalXp] = 119;
        settings.Values[SettingKeys.WorkerLevelCurrentLevel] = 1;
        var service = new WorkerLevelService(settings);

        var snapshot = await service.RecordActionAsync(BuiltInGestureAction.PasteLatestClipboardItem, true, DateTimeOffset.UtcNow, CancellationToken.None);

        Assert.Equal(3, snapshot.CurrentLevel.Level);
        Assert.Equal("粘贴熟练工", snapshot.CurrentLevel.Title);
        Assert.True(snapshot.LeveledUp);
    }

    [Fact]
    public async Task GetSnapshotAsync_handles_damaged_settings()
    {
        var settings = new FakeSettingsService();
        settings.Values[SettingKeys.WorkerLevelTotalXp] = -99;
        settings.Values[SettingKeys.WorkerLevelCurrentLevel] = 99;
        settings.Values[SettingKeys.WorkerLevelTotalActionCount] = -12;
        var service = new WorkerLevelService(settings);

        var snapshot = await service.GetSnapshotAsync(CancellationToken.None);

        Assert.Equal(1, snapshot.CurrentLevel.Level);
        Assert.Equal(0, snapshot.TotalXp);
        Assert.Equal(0, snapshot.TotalActionCount);
    }
}
