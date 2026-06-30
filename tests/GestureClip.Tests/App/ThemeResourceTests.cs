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
        Assert.Contains("GlassCardStyle", glass);
    }

    [Fact]
    public void ClipboardOverlayWindow_uses_glass_panel_styling()
    {
        var path = FindRepositoryFile("src", "GestureClip.App", "ClipboardOverlayWindow.xaml");
        var xaml = File.ReadAllText(path);

        Assert.Contains("AllowsTransparency=\"True\"", xaml);
        Assert.Contains("Style=\"{StaticResource GlassPanelStyle}\"", xaml);
        Assert.Contains("ShortcutNumberConverter", xaml);
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
