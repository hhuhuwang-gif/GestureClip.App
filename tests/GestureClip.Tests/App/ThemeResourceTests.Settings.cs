using Xunit;
using GestureClip.Tests.TestDoubles;

namespace GestureClip.Tests.App;

public sealed partial class ThemeResourceTests
{

    [Fact]
    public void SettingsWindow_keeps_readonly_textbox_bindings_one_way()
    {
        var path = FindRepositoryFile("src", "GestureClip.App", "SettingsWindow.xaml");
        var xaml = File.ReadAllText(path);

        Assert.Contains("Text=\"{Binding DatabasePath, Mode=OneWay}\"", xaml);
        Assert.Contains("Text=\"{Binding LogDirectory, Mode=OneWay}\"", xaml);
        Assert.Contains("Text=\"{Binding DiagnosticsText, Mode=OneWay}\"", xaml);
        Assert.Contains("导出诊断包", xaml);
        Assert.Contains("ExportDiagnosticsCommand", xaml);
        Assert.DoesNotContain("Text=\"{Binding DatabasePath}\"", xaml);
        Assert.DoesNotContain("Text=\"{Binding LogDirectory}\"", xaml);
        Assert.DoesNotContain("Text=\"{Binding DiagnosticsText}\"", xaml);
    }

    [Fact]
    public void SettingsWindow_uses_custom_rounded_shell_and_gesture_customization_controls()
    {
        var path = FindRepositoryFile("src", "GestureClip.App", "SettingsWindow.xaml");
        var xaml = File.ReadAllText(path);

        Assert.Contains("WindowStyle=\"None\"", xaml);
        Assert.Contains("AllowsTransparency=\"True\"", xaml);
        Assert.Contains("CornerRadius=\"14\"", xaml);
        Assert.Contains("BrushAppBackground", xaml);
        Assert.Contains("TrafficLightButtonStyle", xaml);
        Assert.Contains("SettingsSearchBox", xaml);
        Assert.Contains("搜索设置", xaml);
        Assert.Contains("NavHome", xaml);
        Assert.Contains("常用", xaml);
        Assert.Contains("ToggleRailButton", xaml);
        Assert.Contains("RightRailBorder", xaml);
        Assert.Contains("GestureStrokeColorOptions", xaml);
        Assert.Contains("NewGesturePattern", xaml);
        Assert.Contains("AddCustomGestureBindingCommand", xaml);
        Assert.Contains("SettingRowStyle", xaml);
        Assert.Contains("OpenClipboardHotkeyText", xaml);
        Assert.DoesNotContain("TabItem Header=\"数据与清理\"", xaml);
    }

    [Fact]
    public void SettingsWindow_uses_larger_window_controls_and_custom_scrollbars()
    {
        var settingsPath = FindRepositoryFile("src", "GestureClip.App", "SettingsWindow.xaml");
        var workstationPath = FindRepositoryFile("src", "GestureClip.App", "WorkstationDashboardWindow.xaml");
        var controlsPath = FindRepositoryFile("src", "GestureClip.App", "Themes", "Controls.xaml");
        var settings = File.ReadAllText(settingsPath);
        var workstation = RepositoryXaml.Read(workstationPath);
        var controls = File.ReadAllText(controlsPath);

        Assert.Contains("TrafficLightButtonStyle", settings);
        Assert.Contains("BrushTrafficClose", settings);
        Assert.Contains("BrushTrafficYellow", settings);
        Assert.Contains("BrushTrafficGreen", settings);
        Assert.Contains("MaximizeButton_Click", settings);
        Assert.Contains("MinimizeButton_Click", settings);
        Assert.Contains("CloseButton_Click", workstation);
        Assert.Contains("MinimizeButton_Click", workstation);
        Assert.Contains("MaximizeButton_Click", workstation);
        Assert.Contains("GlassScrollBarStyle", controls);
        Assert.Contains("GlassScrollViewerStyle", controls);
        Assert.Contains("ScrollBarThumb", controls);
        Assert.Contains("CornerRadius=\"4\"", controls);
        Assert.Contains("Value=\"6\"", controls);
    }

    [Fact]
    public void SettingsWindow_uses_consistent_plain_language_intro_cards()
    {
        var path = FindRepositoryFile("src", "GestureClip.App", "SettingsWindow.xaml");
        var xaml = File.ReadAllText(path);

        Assert.Contains("SettingsIntroCardStyle", xaml);
        Assert.Contains("SettingsIntroTitleStyle", xaml);
        Assert.Contains("HomePageIntro", xaml);
        Assert.Contains("ClipboardPageIntro", xaml);
        Assert.Contains("GesturePageIntro", xaml);
        Assert.Contains("PrivacyPageIntro", xaml);
        Assert.Contains("StartupPageIntro", xaml);
        Assert.Contains("WorkstationPageIntro", xaml);
        Assert.Contains("DiagnosticsPageIntro", xaml);
        Assert.Contains("AboutPageIntro", xaml);
        Assert.Contains("确认权限、数据位置与运行状态", xaml);
        Assert.Contains("控制是否记录历史", xaml);
        Assert.Contains("去设计 / 更换动作", xaml);
        Assert.Contains("规则只在本机生效", xaml);
        Assert.Contains("关闭窗口仍会隐藏到托盘", xaml);
        Assert.Contains("所有统计仍然只在本地计算", xaml);
        Assert.Contains("遇到问题时先复制诊断信息", xaml);
        Assert.Contains("检查更新或执行一键覆盖更新", xaml);
    }

    [Fact]
    public void SettingsWindow_unifies_button_widths_spacing_and_danger_zone_contract()
    {
        var path = FindRepositoryFile("src", "GestureClip.App", "SettingsWindow.xaml");
        var xaml = File.ReadAllText(path);

        Assert.Contains("SettingsFormRowStyle", xaml);
        Assert.Contains("BasedOn=\"{StaticResource SettingRowStyle}\"", xaml);
        Assert.Contains("SettingsListContainerStyle", xaml);
        Assert.Contains("SettingsSecondaryActionButtonStyle", xaml);
        Assert.Contains("SettingsPrimaryActionButtonStyle", xaml);
        Assert.Contains("SettingsDangerActionButtonStyle", xaml);
        Assert.Contains("SettingsDangerTextButtonStyle", xaml);
        Assert.Contains("SettingsCompactActionButtonStyle", xaml);
        Assert.Contains("SettingsGestureTemplateButtonStyle", xaml);
        Assert.Contains("SettingsHotkeyBoxStyle", xaml);
        Assert.Contains("Text=\"危险操作\"", xaml);
        Assert.Contains("会影响剪贴板历史，执行前请确认。", xaml);
        Assert.Contains("Command=\"{Binding ClearAllClipboardItemsCommand}\"", xaml);
        Assert.Contains("Style=\"{StaticResource SettingsDangerTextButtonStyle}\"", xaml);
        Assert.Contains("Style=\"{StaticResource SettingsSecondaryActionButtonStyle}\"", xaml);
        Assert.Contains("Style=\"{StaticResource SettingsCompactActionButtonStyle}\"", xaml);
        Assert.Contains("Style=\"{StaticResource SettingsGestureTemplateButtonStyle}\"", xaml);
        Assert.Contains("Width=\"200\"", xaml);
        Assert.Contains("Width=\"240\"", xaml);
        Assert.Contains("NavColumn", xaml);
        Assert.Contains("RailColumn", xaml);
        Assert.Contains("SettingsSearchBox", xaml);
        Assert.DoesNotContain("Content=\"复制\" MinWidth=\"0\" Width=\"76\"", xaml);
        Assert.DoesNotContain("Content=\"右键+左键  粘贴并回车\" MinWidth=\"0\" Width=\"190\"", xaml);
        Assert.DoesNotContain("Content=\"打开日志目录\" Command=\"{Binding OpenLogDirectoryCommand}\"", xaml);
    }

    [Fact]
    public void SettingsWindow_keeps_gesture_page_simple_with_advanced_settings_expanded_and_left_button_off_by_default()
    {
        var path = FindRepositoryFile("src", "GestureClip.App", "SettingsWindow.xaml");
        var xaml = File.ReadAllText(path);

        Assert.Contains("按住右键，往一个方向划一下，松开右键就会执行动作。", xaml);
        Assert.Contains("GestureAdvancedSettingsExpander", xaml);
        Assert.Contains("EdgeEnhancementPromoCard", xaml);
        Assert.Contains("启用屏幕角落 / 边缘触发", xaml);
        Assert.DoesNotContain("启用左键拖动画手势", xaml);
        Assert.Contains("左键点击可以作为执行动作，但不是手势触发键", xaml);
        Assert.DoesNotContain("左边缘 + 鼠标左键", xaml);
        Assert.Contains("划多远才算手势", xaml);
        Assert.Contains("防误触间隔", xaml);
        Assert.DoesNotContain("停留 ms", xaml);
        Assert.DoesNotContain("冷却 ms", xaml);
    }

    [Fact]
    public void SettingsWindow_exposes_workstation_dashboard_settings()
    {
        var path = FindRepositoryFile("src", "GestureClip.App", "SettingsWindow.xaml");
        var xaml = File.ReadAllText(path);

        Assert.Contains("TabItem Header=\"小熊\"", xaml);
        Assert.Contains("WorkstationEnabled", xaml);
        Assert.Contains("WorkstationMonthlySalary", xaml);
        Assert.Contains("WorkstationWorkStartTime", xaml);
        Assert.Contains("WorkstationWorkEndTime", xaml);
        Assert.Contains("WorkstationWorkdays", xaml);
        Assert.Contains("WorkstationPayday", xaml);
        Assert.Contains("WorkstationShowFishingValue", xaml);
        Assert.Contains("WorkstationDailyReportEnabled", xaml);
        Assert.True(
            xaml.IndexOf("TabItem Header=\"小熊\"", StringComparison.Ordinal) <
            xaml.IndexOf("WorkstationWorkdays", StringComparison.Ordinal));
    }

    [Fact]
    public void SettingsWindow_hides_unused_edge_mouse_button_section()
    {
        var path = FindRepositoryFile("src", "GestureClip.App", "SettingsWindow.xaml");
        var xaml = File.ReadAllText(path);

        Assert.DoesNotContain("边缘 + 鼠标按钮", xaml);
        Assert.DoesNotContain("左边缘 + 鼠标中键", xaml);
        Assert.DoesNotContain("左边缘 + 鼠标侧键 1", xaml);
        Assert.DoesNotContain("左边缘 + 鼠标侧键 2", xaml);
        Assert.DoesNotContain("右上角 + 滚轮", xaml);
    }
}
