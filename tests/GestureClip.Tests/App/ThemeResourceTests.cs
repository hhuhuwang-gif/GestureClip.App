using Xunit;

namespace GestureClip.Tests.App;

public sealed class ThemeResourceTests
{
    [Fact]
    public void App_merges_glass_theme_resource_dictionaries()
    {
        var appPath = FindRepositoryFile("src", "GestureClip.App", "App.xaml");
        var appXaml = File.ReadAllText(appPath);

        Assert.Contains("Themes/Colors.xaml", appXaml);
        Assert.Contains("Themes/Brushes.xaml", appXaml);
        Assert.Contains("Themes/Controls.xaml", appXaml);
        Assert.Contains("Themes/GlassStyles.xaml", appXaml);
    }

    [Fact]
    public void Theme_resources_define_core_glass_styles()
    {
        var controlsPath = FindRepositoryFile("src", "GestureClip.App", "Themes", "Controls.xaml");
        var glassPath = FindRepositoryFile("src", "GestureClip.App", "Themes", "GlassStyles.xaml");
        var controls = File.ReadAllText(controlsPath);
        var glass = File.ReadAllText(glassPath);

        Assert.Contains("PrimaryButtonStyle", controls);
        Assert.Contains("SecondaryButtonStyle", controls);
        Assert.Contains("DangerButtonStyle", controls);
        Assert.Contains("GlassTextBoxStyle", controls);
        Assert.Contains("GlassComboBoxStyle", controls);
        Assert.Contains("GlassCheckBoxStyle", controls);
        Assert.Contains("GlassListBoxStyle", controls);
        Assert.Contains("GlassTabItemStyle", controls);
        Assert.Contains("GlassCardStyle", glass);
    }

    [Fact]
    public void Theme_uses_readable_soft_macos_inspired_palette()
    {
        var colorsPath = FindRepositoryFile("src", "GestureClip.App", "Themes", "Colors.xaml");
        var colors = File.ReadAllText(colorsPath);

        Assert.Contains("#F6F7FB", colors);
        Assert.Contains("#FFFFFFFF", colors);
        Assert.Contains("#17182733", colors);
        Assert.Contains("#15171D", colors);
        Assert.Contains("#5E6675", colors);
        Assert.Contains("#101011", colors);
        Assert.Contains("#F36D64", colors);
        Assert.Contains("ColorTabSelected", colors);
    }

    [Fact]
    public void ComboBox_theme_defines_readable_dropdown_and_item_states()
    {
        var controlsPath = FindRepositoryFile("src", "GestureClip.App", "Themes", "Controls.xaml");
        var brushesPath = FindRepositoryFile("src", "GestureClip.App", "Themes", "Brushes.xaml");
        var controls = File.ReadAllText(controlsPath);
        var brushes = File.ReadAllText(brushesPath);

        Assert.Contains("GlassComboBoxItemStyle", controls);
        Assert.Contains("PART_Popup", controls);
        Assert.Contains("BrushComboBoxDropDown", brushes);
        Assert.Contains("BrushControlHover", brushes);
        Assert.Contains("BrushControlSelected", brushes);
        Assert.Contains("IsHighlighted", controls);
        Assert.Contains("IsSelected", controls);
    }

    [Fact]
    public void ClipboardOverlayWindow_uses_glass_panel_styling()
    {
        var path = FindRepositoryFile("src", "GestureClip.App", "ClipboardOverlayWindow.xaml");
        var xaml = File.ReadAllText(path);

        Assert.Contains("Width=\"760\"", xaml);
        Assert.Contains("Height=\"520\"", xaml);
        Assert.Contains("AllowsTransparency=\"True\"", xaml);
        Assert.Contains("CornerRadius=\"28\"", xaml);
        Assert.Contains("Background=\"{DynamicResource BrushGlassStrong}\"", xaml);
        Assert.Contains("IsSelected", xaml);
        Assert.Contains("ShortcutNumberConverter", xaml);
    }

    [Fact]
    public void ClipboardOverlayWindow_exposes_detail_action_buttons()
    {
        var path = FindRepositoryFile("src", "GestureClip.App", "ClipboardOverlayWindow.xaml");
        var xaml = File.ReadAllText(path);

        Assert.Contains("x:Name=\"DetailCopyButton\"", xaml);
        Assert.Contains("x:Name=\"DetailPasteButton\"", xaml);
        Assert.Contains("x:Name=\"DetailPinButton\"", xaml);
        Assert.Contains("x:Name=\"DetailFavoriteButton\"", xaml);
        Assert.Contains("x:Name=\"DetailDeleteButton\"", xaml);
    }

    [Fact]
    public void ClipboardOverlayWindow_exposes_per_item_quick_action_buttons()
    {
        var xamlPath = FindRepositoryFile("src", "GestureClip.App", "ClipboardOverlayWindow.xaml");
        var sourcePath = FindRepositoryFile("src", "GestureClip.App", "ClipboardOverlayWindow.xaml.cs");
        var xaml = File.ReadAllText(xamlPath);
        var source = File.ReadAllText(sourcePath);

        Assert.Contains("x:Name=\"ItemQuickCopyButton\"", xaml);
        Assert.Contains("x:Name=\"ItemQuickPasteButton\"", xaml);
        Assert.Contains("x:Name=\"ItemQuickPinButton\"", xaml);
        Assert.Contains("x:Name=\"ItemQuickDeleteButton\"", xaml);
        Assert.Contains("QuickCopyItemButton_Click", source);
        Assert.Contains("QuickPasteItemButton_Click", source);
        Assert.Contains("QuickPinItemButton_Click", source);
        Assert.Contains("QuickDeleteItemButton_Click", source);
        Assert.Contains("SelectSingleItem", source);
    }

    [Fact]
    public void ClipboardOverlayWindow_supports_search_friendly_keyboard_shortcuts()
    {
        var path = FindRepositoryFile("src", "GestureClip.App", "ClipboardOverlayWindow.xaml.cs");
        var source = File.ReadAllText(path);

        Assert.Contains("ClearSearchAsync", source);
        Assert.Contains("Key.F", source);
        Assert.Contains("FocusSearchBox", source);
    }

    [Fact]
    public void ClipboardOverlayWindow_exposes_clear_search_button()
    {
        var xamlPath = FindRepositoryFile("src", "GestureClip.App", "ClipboardOverlayWindow.xaml");
        var sourcePath = FindRepositoryFile("src", "GestureClip.App", "ClipboardOverlayWindow.xaml.cs");
        var xaml = File.ReadAllText(xamlPath);
        var source = File.ReadAllText(sourcePath);

        Assert.Contains("x:Name=\"ClearSearchButton\"", xaml);
        Assert.Contains("Click=\"ClearSearchButton_Click\"", xaml);
        Assert.Contains("ClearSearchButton_Click", source);
        Assert.Contains("ClearSearchAsync", source);
    }

    [Fact]
    public void ClipboardOverlayWindow_supports_fast_office_keyboard_shortcuts()
    {
        var path = FindRepositoryFile("src", "GestureClip.App", "ClipboardOverlayWindow.xaml.cs");
        var source = File.ReadAllText(path);

        Assert.Contains("Key.S", source);
        Assert.Contains("ToggleSelectedFavoriteAsync", source);
        Assert.Contains("Key.P", source);
        Assert.Contains("ToggleSelectedPinnedAsync", source);
        Assert.Contains("SelectFilterByShortcut", source);
    }

    [Fact]
    public void ClipboardOverlayWindow_shows_office_shortcut_hints()
    {
        var path = FindRepositoryFile("src", "GestureClip.App", "ClipboardOverlayWindow.xaml");
        var xaml = File.ReadAllText(path);

        Assert.Contains("Ctrl + 1-5", xaml);
        Assert.Contains("Ctrl + P", xaml);
        Assert.Contains("Ctrl + S", xaml);
        Assert.Contains("Delete", xaml);
    }

    [Fact]
    public void ClipboardOverlayWindow_shows_selection_summary()
    {
        var xamlPath = FindRepositoryFile("src", "GestureClip.App", "ClipboardOverlayWindow.xaml");
        var sourcePath = FindRepositoryFile("src", "GestureClip.App", "ClipboardOverlayWindow.xaml.cs");
        var xaml = File.ReadAllText(xamlPath);
        var source = File.ReadAllText(sourcePath);

        Assert.Contains("Text=\"{Binding SummaryText}\"", xaml);
        Assert.Contains("SelectionChanged=\"HistoryList_SelectionChanged\"", xaml);
        Assert.Contains("UpdateSelectedCount", source);
    }

    [Fact]
    public void MouseGestureService_does_not_reinsert_start_point_when_trimming_overlay_points()
    {
        var path = FindRepositoryFile("src", "GestureClip.Features", "Gestures", "MouseGestureService.cs");
        var source = File.ReadAllText(path);

        Assert.Contains("RemoveRange(0, overflow)", source);
        Assert.DoesNotContain(".Insert(0", source);
        Assert.DoesNotContain("_startPoint.Value", source);
    }

    [Fact]
    public void SettingsWindow_keeps_readonly_textbox_bindings_one_way()
    {
        var path = FindRepositoryFile("src", "GestureClip.App", "SettingsWindow.xaml");
        var xaml = File.ReadAllText(path);

        Assert.Contains("Text=\"{Binding DatabasePath, Mode=OneWay}\"", xaml);
        Assert.Contains("Text=\"{Binding LogDirectory, Mode=OneWay}\"", xaml);
        Assert.Contains("Text=\"{Binding DiagnosticsText, Mode=OneWay}\"", xaml);
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
        Assert.Contains("CornerRadius=\"28\"", xaml);
        Assert.Contains("BrushDarkPanel", xaml);
        Assert.Contains("搜索设置稍后开放", xaml);
        Assert.Contains("GestureStrokeColorOptions", xaml);
        Assert.Contains("NewGesturePattern", xaml);
        Assert.Contains("AddCustomGestureBindingCommand", xaml);
        Assert.Contains("SettingRowStyle", xaml);
        Assert.Contains("OpenClipboardHotkeyText", xaml);
        Assert.DoesNotContain("TabItem Header=\"数据与清理\"", xaml);
    }

    [Fact]
    public void SettingsWindow_keeps_gesture_page_simple_with_advanced_settings_expanded_without_left_button_trigger()
    {
        var path = FindRepositoryFile("src", "GestureClip.App", "SettingsWindow.xaml");
        var xaml = File.ReadAllText(path);

        Assert.Contains("按住右键，往一个方向划一下，松开右键就会执行动作。", xaml);
        Assert.Contains("Header=\"高级设置\"", xaml);
        Assert.Contains("IsExpanded=\"True\"", xaml);
        Assert.DoesNotContain("左边缘 + 鼠标左键", xaml);
        Assert.Contains("划多远才算手势", xaml);
        Assert.Contains("防误触间隔", xaml);
        Assert.DoesNotContain("停留 ms", xaml);
        Assert.DoesNotContain("冷却 ms", xaml);
    }

    private static string FindRepositoryFile(params string[] segments)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(new[] { directory.FullName }.Concat(segments).ToArray());
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("Could not locate repository file.", Path.Combine(segments));
    }
}
