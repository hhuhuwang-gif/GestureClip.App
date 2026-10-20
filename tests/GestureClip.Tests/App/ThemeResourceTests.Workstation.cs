using Xunit;
using GestureClip.Tests.TestDoubles;

namespace GestureClip.Tests.App;

public sealed partial class ThemeResourceTests
{

    [Fact]
    public void WorkstationDashboardWindow_uses_glass_cards_and_plain_language_metrics()
    {
        var path = FindRepositoryFile("src", "GestureClip.App", "WorkstationDashboardWindow.xaml");
        var xaml = File.ReadAllText(path);

        Assert.Contains("工位小熊", xaml);
        Assert.Contains("v0.6.41 · 本地", xaml);
        Assert.Contains("UniformGrid", xaml);
        Assert.Contains("OffWorkCountdownText", xaml);
        Assert.Contains("TodayEarnedText", xaml);
        Assert.Contains("TodayFishingValueText", xaml);
        Assert.Contains("ActionStatsText", xaml);
        Assert.Contains("Microsoft YaHei UI", xaml);
        Assert.Contains("GreetingText", xaml);
        Assert.Contains("StageAccentBrush", xaml);
        Assert.Contains("WorkdayProgress", xaml);
        Assert.Contains("HubThinProgressBarStyle", xaml);
        Assert.Contains("HubFishingBannerStyle", xaml);
        Assert.Contains("HubReportTextStyle", xaml);
        Assert.Contains("ShowEmptySalaryGuide", xaml);
        Assert.Contains("OpenWorkRulesCommand", xaml);
        Assert.Contains("FishingStatusBanner", xaml);
        Assert.Contains("Esc 关闭", xaml);
        Assert.Contains("DailyReportDisplayText", xaml);
        Assert.Contains("GlassScrollViewerStyle", xaml);
        Assert.DoesNotContain("#FBFBFD", xaml);
        Assert.DoesNotContain("#EAF3FF", xaml);
    }

    [Fact]
    public void WorkstationDashboard_has_tray_and_lifecycle_entry()
    {
        var lifecyclePath = FindRepositoryFile("src", "GestureClip.App", "Services", "AppLifecycleService.cs");
        var trayPath = FindRepositoryFile("src", "GestureClip.App", "Services", "TrayIconService.cs");
        var lifecycle = File.ReadAllText(lifecyclePath);
        var tray = File.ReadAllText(trayPath);

        Assert.Contains("ShowWorkstationDashboardWindow", lifecycle);
        Assert.Contains("WorkstationDashboardWindow", lifecycle);
        Assert.Contains("工位小熊", tray);
        Assert.Contains("ShowWorkstationDashboardWindow", tray);
    }

    [Fact]
    public void TrayIconService_respects_workstation_enabled_setting_for_dashboard_entry()
    {
        var path = FindRepositoryFile("src", "GestureClip.App", "Services", "TrayIconService.cs");
        var source = File.ReadAllText(path);

        Assert.Contains("ISettingsService", source);
        Assert.Contains("SettingKeys.WorkstationEnabled", source);
        Assert.Contains("ShowWorkstationDashboardWindow", source);
    }

    [Fact]
    public void MainWindow_exposes_workstation_dashboard_button()
    {
        var xamlPath = FindRepositoryFile("src", "GestureClip.App", "MainWindow.xaml");
        var sourcePath = FindRepositoryFile("src", "GestureClip.App", "MainWindow.xaml.cs");
        var xaml = File.ReadAllText(xamlPath);
        var source = File.ReadAllText(sourcePath);

        Assert.Contains("工位小熊", xaml);
        Assert.Contains("OpenWorkstationButton_Click", xaml);
        Assert.Contains("ShowWorkstationDashboardWindow", source);
    }

    [Fact]
    public void Workstation_settings_show_templates_and_live_preview()
    {
        var path = FindRepositoryFile("src", "GestureClip.App", "SettingsWindow.xaml");
        var xaml = File.ReadAllText(path);

        Assert.Contains("工作制模板", xaml);
        Assert.Contains("实时预览", xaml);
        Assert.Contains("WorkstationTemplateOptions", xaml);
        Assert.Contains("ApplyWorkstationTemplateCommand", xaml);
        Assert.Contains("WorkstationPreviewTodayEarnedText", xaml);
        Assert.Contains("WorkstationPreviewOffWorkText", xaml);
        Assert.Contains("WorkstationPreviewPaydayText", xaml);
    }

    [Fact]
    public void Workstation_settings_group_display_options_and_copywriting_style()
    {
        var path = FindRepositoryFile("src", "GestureClip.App", "SettingsWindow.xaml");
        var xaml = File.ReadAllText(path);

        Assert.Contains("基础显示", xaml);
        Assert.Contains("趣味显示", xaml);
        Assert.Contains("统计显示", xaml);
        Assert.Contains("文案风格", xaml);
        Assert.Contains("WorkstationCopywritingStyle", xaml);
        Assert.Contains("正常模式", xaml);
        Assert.Contains("打工人模式", xaml);
        Assert.Contains("抽象模式", xaml);
    }

    [Fact]
    public void Workstation_settings_exposes_overwork_reminder_controls()
    {
        var path = FindRepositoryFile("src", "GestureClip.App", "SettingsWindow.xaml");
        var xaml = File.ReadAllText(path);

        Assert.Contains("猝死提醒", xaml);
        Assert.Contains("开启过劳提醒", xaml);
        Assert.Contains("WorkstationEnableOverworkReminder", xaml);
        Assert.Contains("WorkstationOverworkReminderIntervalMinutes", xaml);
        Assert.Contains("WorkstationOverworkHighRiskAfterHours", xaml);
        Assert.Contains("WorkstationEnableHudTimeColor", xaml);
        Assert.Contains("WorkstationEnableStrongOverworkWarning", xaml);
        Assert.Contains("WorkstationOverworkReminderCanSnooze", xaml);
        Assert.Contains("WorkstationOverworkSnoozeMinutes", xaml);
        Assert.Contains("OverworkPreviewStageText", xaml);
        Assert.Contains("OverworkPreviewHudColorText", xaml);
        Assert.Contains("OverworkPreviewNextReminderText", xaml);
        Assert.Contains("OverworkPreviewWorkedText", xaml);
    }
}
