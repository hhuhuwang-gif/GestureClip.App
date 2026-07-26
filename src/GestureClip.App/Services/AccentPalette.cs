using System.Windows.Media;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;

namespace GestureClip.App.Services;

/// <summary>
/// Derives the accent color override set (Color* resource keys) from a single base accent.
/// Pure math so it can be unit-tested without a running Application.
/// </summary>
public static class AccentPalette
{
    /// <summary>Dark-surface tone used to soften accents in dark mode; matches ColorSurfaceDark in Colors.Dark.xaml.</summary>
    private static readonly Color DarkSurface = Color.FromRgb(0x15, 0x1C, 0x2C);

    public static bool TryParse(string? hex, out Color color)
    {
        color = default;
        if (string.IsNullOrWhiteSpace(hex))
        {
            return false;
        }

        try
        {
            if (ColorConverter.ConvertFromString(hex.Trim()) is Color parsed)
            {
                color = parsed;
                return true;
            }
        }
        catch (FormatException)
        {
        }

        return false;
    }

    public static Color Blend(Color from, Color to, double amount)
    {
        var t = Math.Clamp(amount, 0d, 1d);
        return Color.FromArgb(
            (byte)(from.A + (to.A - from.A) * t),
            (byte)(from.R + (to.R - from.R) * t),
            (byte)(from.G + (to.G - from.G) * t),
            (byte)(from.B + (to.B - from.B) * t));
    }

    public static Color Lighten(Color color, double amount) => Blend(color, Colors.White, amount);

    public static Color Darken(Color color, double amount) => Blend(color, Colors.Black, amount);

    public static Color WithAlpha(Color color, byte alpha) => Color.FromArgb(alpha, color.R, color.G, color.B);

    /// <summary>The accent actually painted for bright foreground uses (lightened in dark mode for contrast).</summary>
    public static Color BrightAccent(Color accent, bool isDark) => isDark ? Lighten(accent, 0.25) : accent;

    public static IReadOnlyDictionary<string, Color> BuildColorOverrides(Color accent, bool isDark)
    {
        var bright = BrightAccent(accent, isDark);
        return isDark
            ? new Dictionary<string, Color>
            {
                ["ColorPrimaryBright"] = bright,
                ["ColorPrimarySoft"] = WithAlpha(accent, 0x33),
                ["ColorFocusRing"] = WithAlpha(bright, 0x55),
                ["ColorAccentSoft"] = Blend(accent, DarkSurface, 0.70),
                ["ColorControlSelected"] = Blend(accent, DarkSurface, 0.75),
                ["ColorListSelectedHover"] = Blend(accent, DarkSurface, 0.70),
                ["ColorBrandSky"] = bright
            }
            : new Dictionary<string, Color>
            {
                ["ColorPrimaryBright"] = accent,
                ["ColorPrimarySoft"] = WithAlpha(accent, 0x1A),
                ["ColorFocusRing"] = WithAlpha(accent, 0x40),
                ["ColorAccentSoft"] = WithAlpha(Lighten(accent, 0.78), 0xB8),
                ["ColorControlSelected"] = WithAlpha(Lighten(accent, 0.82), 0xCC),
                ["ColorListSelectedHover"] = WithAlpha(Lighten(accent, 0.78), 0xCC),
                ["ColorBrandSky"] = accent
            };
    }
}
