using System.Windows.Media;
using GestureClip.App.Services;
using Xunit;

namespace GestureClip.Tests.App;

public sealed class AccentButtonContrastTests
{
    private const double MinimumWhiteTextContrast = 4.5;
    private const double ContrastLuminanceOffset = 0.05;
    private const double WhiteLuminance = 1;
    private const double SrgbLinearThreshold = 0.04045;
    private const double SrgbLinearScale = 12.92;
    private const double SrgbOffset = 0.055;
    private const double SrgbScale = 1.055;
    private const double SrgbExponent = 2.4;
    private const double RedLuminanceWeight = 0.2126;
    private const double GreenLuminanceWeight = 0.7152;
    private const double BlueLuminanceWeight = 0.0722;

    public static IEnumerable<object[]> AccentCases()
    {
        string[] accents =
        [
            "#3B82F6", "#8B5CF6", "#EC4899", "#EF4444", "#F97316", "#16A34A", "#0D9488", "#475569",
            "#FFFFFF", "#FFFFCC", "#FFFF00", "#000000", "#FF0000", "#00FF00", "#0000FF", "#00FFFFFF"
        ];
        foreach (var accent in accents)
        {
            yield return [accent, false];
            yield return [accent, true];
        }
    }

    [Theory]
    [MemberData(nameof(AccentCases))]
    public void Accent_button_states_support_normal_white_text(string hex, bool isDark)
    {
        Assert.True(AccentPalette.TryParse(hex, out var accent));

        var colors = AccentPalette.BuildColorOverrides(accent, isDark);

        foreach (var key in new[] { "ColorPrimaryFill", "ColorPrimaryFillHover", "ColorPrimaryFillPressed" })
        {
            Assert.Equal(byte.MaxValue, colors[key].A);
            var contrast = WhiteTextContrast(colors[key]);
            Assert.True(contrast >= MinimumWhiteTextContrast, $"{hex}, dark={isDark}, {key}: {contrast:F4}:1");
        }
        Assert.Equal(AccentPalette.BrightAccent(accent, isDark), colors["ColorPrimaryBright"]);
    }

    [Fact]
    public void Already_readable_accent_keeps_its_rgb_values()
    {
        var accent = Color.FromRgb(0x47, 0x55, 0x69);

        Assert.Equal(accent, AccentPalette.ButtonFill(accent));
    }

    private static double WhiteTextContrast(Color color)
    {
        // Independent WCAG relative luminance calculation verifies the painted RGB values.
        var luminance = RedLuminanceWeight * LinearChannel(color.R) + GreenLuminanceWeight * LinearChannel(color.G)
            + BlueLuminanceWeight * LinearChannel(color.B);
        return (WhiteLuminance + ContrastLuminanceOffset) / (luminance + ContrastLuminanceOffset);
    }

    private static double LinearChannel(byte channel)
    {
        var srgb = channel / (double)byte.MaxValue;
        return srgb <= SrgbLinearThreshold
            ? srgb / SrgbLinearScale
            : Math.Pow((srgb + SrgbOffset) / SrgbScale, SrgbExponent);
    }
}
