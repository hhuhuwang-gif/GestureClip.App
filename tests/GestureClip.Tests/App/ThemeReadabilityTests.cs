using System.Windows.Media;
using System.Xml.Linq;
using Xunit;

namespace GestureClip.Tests.App;

public sealed class ThemeReadabilityTests
{
    private const double MinimumTextContrast = 4.5;
    private static readonly XNamespace Xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

    [Theory]
    [InlineData("Colors.xaml")]
    [InlineData("Colors.Dark.xaml")]
    public void Text_and_primary_buttons_are_readable_on_actual_surface_stops(string theme)
    {
        var palette = ReadTheme(theme).Root!.Elements().Where(element => element.Name.LocalName == "Color")
            .ToDictionary(element => element.Attribute(Xaml + "Key")!.Value,
                element => (Color)ColorConverter.ConvertFromString(element.Value));
        string[] foregrounds = ["ColorTextPrimaryDark", "ColorTextSecondaryDark", "ColorTextMutedDark"];
        string[] surfaces = ["ColorCardElevated", "ColorCardBottom", "ColorSidebarTop", "ColorSidebarBottom",
            "ColorContentTop", "ColorContentBottom", "ColorGlassTop", "ColorGlassMid", "ColorGlassBottom",
            "ColorGlassStrongDark", "ColorOverlayTop", "ColorOverlayBottom"];
        foreach (var foreground in foregrounds)
        foreach (var surface in surfaces)
        {
            var ratio = Contrast(palette[foreground], palette[surface]);
            Assert.True(ratio >= MinimumTextContrast, $"{theme} {foreground} on {surface}: {ratio:F2}:1");
        }
        foreach (var fill in new[] { "ColorPrimaryFill", "ColorPrimaryFillHover", "ColorPrimaryFillPressed" })
            Assert.True(Contrast(Colors.White, palette[fill]) >= MinimumTextContrast, $"{theme}: {fill}");
    }

    [Fact]
    public void Shared_surfaces_resolve_colors_from_both_theme_dictionaries()
    {
        string[] names = ["BrushSidebarBackground", "BrushGlassPanel", "BrushSettingsContentPanel", "BrushGlassCard"];
        var brushes = ReadTheme("Brushes.xaml").Root!.Elements()
            .Where(element => names.Contains(element.Attribute(Xaml + "Key")?.Value)).ToArray();
        Assert.Equal(names.Length, brushes.Length);
        foreach (var theme in new[] { "Colors.xaml", "Colors.Dark.xaml" })
        {
            var keys = ReadTheme(theme).Root!.Elements().Select(element => element.Attribute(Xaml + "Key")?.Value).ToHashSet();
            foreach (var stop in brushes.SelectMany(brush => brush.Elements()))
            {
                var reference = stop.Attribute("Color")!.Value;
                Assert.StartsWith("{StaticResource ", reference);
                Assert.Contains(reference[16..^1], keys);
            }
        }
    }

    private static double Contrast(Color first, Color second)
    {
        var a = Luminance(first);
        var b = Luminance(second);
        return (Math.Max(a, b) + 0.05) / (Math.Min(a, b) + 0.05);
    }

    private static double Luminance(Color color)
    {
        static double Linear(byte channel)
        {
            var value = channel / 255d;
            return value <= 0.04045 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);
        }
        return Linear(color.R) * 0.2126 + Linear(color.G) * 0.7152 + Linear(color.B) * 0.0722;
    }

    private static XDocument ReadTheme(string filename)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var path = Path.Combine(directory.FullName, "src", "GestureClip.App", "Themes", filename);
            if (File.Exists(path)) return XDocument.Load(path);
            directory = directory.Parent;
        }
        throw new FileNotFoundException(filename);
    }
}
