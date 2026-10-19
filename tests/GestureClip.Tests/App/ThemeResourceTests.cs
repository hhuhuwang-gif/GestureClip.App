using Xunit;
using GestureClip.Tests.TestDoubles;

namespace GestureClip.Tests.App;

public sealed partial class ThemeResourceTests
{

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
        var darkPath = FindRepositoryFile("src", "GestureClip.App", "Themes", "Colors.Dark.xaml");
        var brushesPath = FindRepositoryFile("src", "GestureClip.App", "Themes", "Brushes.xaml");
        var colors = File.ReadAllText(colorsPath);
        var dark = File.ReadAllText(darkPath);
        var brushes = File.ReadAllText(brushesPath);

        // Light glass: snow / cloud / slate / sky-blue
        Assert.Contains("#F4F7FB", colors);
        Assert.Contains("#E8ECF1", colors);
        Assert.Contains("#94A3B8", colors);
        Assert.Contains("#3B82F6", colors);
        Assert.Contains("#0F172A", colors);
        Assert.Contains("ColorBrandSnow", colors);
        Assert.Contains("ColorBrandCloud", colors);
        Assert.Contains("ColorBrandSlate", colors);
        Assert.Contains("ColorBrandSky", colors);
        Assert.Contains("ColorGlassLight", colors);
        Assert.Contains("ColorContentBackground", colors);
        // Dark high-contrast slate palette (readable body text)
        Assert.Contains("#0B1220", dark);
        Assert.Contains("#151C2C", dark);
        Assert.Contains("#60A5FA", dark);
        Assert.Contains("#F3F6FC", dark);
        Assert.Contains("ColorAppBg0", colors);
        Assert.Contains("ColorCardElevated", colors);
        Assert.Contains("ColorTabSelected", colors);
        Assert.Contains("ColorRailBackground", colors);
        Assert.Contains("ColorContentBackground", colors);
        Assert.Contains("BrushOverlayPanel", brushes);
        Assert.Contains("BrushAccentSoft", brushes);
        Assert.Contains("BrushSidebarBackground", brushes);
        Assert.Contains("BrushRailBackground", brushes);
        Assert.Contains("BrushGlassPanel", brushes);
        Assert.Contains("BrushGlassCard", brushes);
        Assert.Contains("BrushPrimarySolid", brushes);
        Assert.Contains("TrafficLightButtonStyle", File.ReadAllText(
            FindRepositoryFile("src", "GestureClip.App", "Themes", "GlassStyles.xaml")));
        Assert.Contains("SettingsListContainerStyle", File.ReadAllText(
            FindRepositoryFile("src", "GestureClip.App", "Themes", "GlassStyles.xaml")));

        AssertReadableTextControls();
    }

    private static void AssertReadableTextControls()
    {
        var controls = File.ReadAllText(FindRepositoryFile("src", "GestureClip.App", "Themes", "Controls.xaml"));
        Assert.Contains("PrimaryButtonStyle", controls);
        Assert.Contains("Value=\"#FFFFFF\"", controls);
        Assert.Contains("BrushPrimarySolid", controls);
        // Global TextBlock must not force dark text (breaks white labels on primary buttons).
        var textBlockStyleStart = controls.IndexOf("<Style TargetType=\"TextBlock\">", StringComparison.Ordinal);
        Assert.True(textBlockStyleStart >= 0);
        var textBlockStyleEnd = controls.IndexOf("</Style>", textBlockStyleStart, StringComparison.Ordinal);
        var textBlockStyle = controls.Substring(textBlockStyleStart, textBlockStyleEnd - textBlockStyleStart);
        Assert.DoesNotContain("BrushTextPrimary", textBlockStyle);
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
    public void GlassStyles_define_hub_readable_text_styles()
    {
        var path = FindRepositoryFile("src", "GestureClip.App", "Themes", "GlassStyles.xaml");
        var xaml = File.ReadAllText(path);

        Assert.Contains("HubRootTextStyle", xaml);
        Assert.Contains("HubSectionExpanderStyle", xaml);
        Assert.Contains("HubThinProgressBarStyle", xaml);
        Assert.Contains("HubFishingBannerStyle", xaml);
        Assert.Contains("HubReportTextStyle", xaml);
        Assert.Contains("ControlTemplate", xaml);
        Assert.Contains("Microsoft YaHei UI", xaml);
        Assert.Contains("HubFooterBarStyle", xaml);
        Assert.Contains("HubMetricPanelStyle", xaml);
    }
}
