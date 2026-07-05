using GestureClip.Core.Settings;
using GestureClip.Core.Workstation;
using GestureClip.Features.Workstation;
using GestureClip.Tests.TestDoubles;
using Xunit;

namespace GestureClip.Tests.Workstation;

public sealed class WorkTimeStageServiceTests
{
    [Theory]
    [InlineData("2026-07-06T08:30:00+08:00", WorkTimeStage.BeforeWork)]
    [InlineData("2026-07-06T09:30:00+08:00", WorkTimeStage.EarlyWork)]
    [InlineData("2026-07-06T12:30:00+08:00", WorkTimeStage.LunchBreak)]
    [InlineData("2026-07-06T14:30:00+08:00", WorkTimeStage.MidWork)]
    [InlineData("2026-07-06T17:10:00+08:00", WorkTimeStage.LateWork)]
    [InlineData("2026-07-06T18:30:00+08:00", WorkTimeStage.Overtime)]
    [InlineData("2026-07-05T10:00:00+08:00", WorkTimeStage.RestDay)]
    public void GetSnapshot_returns_expected_work_time_stage(string nowText, WorkTimeStage expected)
    {
        var settings = new FakeSettingsService();
        settings.Values[SettingKeys.WorkstationWorkStartTime] = "09:00";
        settings.Values[SettingKeys.WorkstationWorkEndTime] = "18:00";
        settings.Values[SettingKeys.WorkstationLunchStartTime] = "12:00";
        settings.Values[SettingKeys.WorkstationLunchEndTime] = "13:00";
        settings.Values[SettingKeys.WorkstationWorkdays] = "1,2,3,4,5";
        var service = new WorkTimeStageService(settings);

        var snapshot = service.GetSnapshot(DateTimeOffset.Parse(nowText));

        Assert.Equal(expected, snapshot.Stage);
    }

    [Theory]
    [InlineData(WorkTimeStage.EarlyWork, "GreenGradient", "开工状态")]
    [InlineData(WorkTimeStage.MidWork, "BlueGradient", "稳定输出")]
    [InlineData(WorkTimeStage.LateWork, "RedGradient", "注意休息")]
    [InlineData(WorkTimeStage.Overtime, "DeepRedGradient", "加班中")]
    [InlineData(WorkTimeStage.LunchBreak, "YellowGradient", "午休中")]
    [InlineData(WorkTimeStage.RestDay, "NeutralGradient", "休息日")]
    public void GetTheme_returns_centralized_hud_theme(WorkTimeStage stage, string expectedKey, string expectedStatus)
    {
        var theme = WorkTimeStageThemeProvider.GetTheme(stage);

        Assert.Equal(expectedKey, theme.Key);
        Assert.Equal(expectedStatus, theme.ShortStatusText);
        Assert.StartsWith("#", theme.StartColor, StringComparison.Ordinal);
        Assert.StartsWith("#", theme.EndColor, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(WorkTimeStage.EarlyWork)]
    [InlineData(WorkTimeStage.MidWork)]
    [InlineData(WorkTimeStage.LateWork)]
    [InlineData(WorkTimeStage.Overtime)]
    [InlineData(WorkTimeStage.LunchBreak)]
    [InlineData(WorkTimeStage.RestDay)]
    [InlineData(WorkTimeStage.BeforeWork)]
    [InlineData(WorkTimeStage.OffWork)]
    public void Hud_theme_uses_dark_readable_backgrounds(WorkTimeStage stage)
    {
        var theme = WorkTimeStageThemeProvider.GetTheme(stage);

        Assert.True(IsDarkColor(theme.StartColor), $"{stage} start color should be dark enough for white text.");
        Assert.True(IsDarkColor(theme.EndColor), $"{stage} end color should be dark enough for white text.");
    }

    [Fact]
    public void Invalid_time_settings_fall_back_safely()
    {
        var settings = new FakeSettingsService();
        settings.Values[SettingKeys.WorkstationWorkStartTime] = "bad";
        settings.Values[SettingKeys.WorkstationWorkEndTime] = "also bad";
        settings.Values[SettingKeys.WorkstationWorkdays] = "1,2,3,4,5";
        var service = new WorkTimeStageService(settings);

        var snapshot = service.GetSnapshot(DateTimeOffset.Parse("2026-07-06T09:30:00+08:00"));

        Assert.Equal(WorkTimeStage.EarlyWork, snapshot.Stage);
    }

    private static bool IsDarkColor(string colorText)
    {
        var hex = colorText.TrimStart('#');
        var offset = hex.Length == 8 ? 2 : 0;
        var red = Convert.ToInt32(hex.Substring(offset, 2), 16);
        var green = Convert.ToInt32(hex.Substring(offset + 2, 2), 16);
        var blue = Convert.ToInt32(hex.Substring(offset + 4, 2), 16);
        var luminance = (0.2126 * red + 0.7152 * green + 0.0722 * blue) / 255d;
        return luminance < 0.42;
    }
}
