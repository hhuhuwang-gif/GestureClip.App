using System.Windows;
using System.Windows.Media;
using Color = System.Windows.Media.Color;
using Point = System.Windows.Point;

namespace GestureClip.App.Services;

public sealed partial class AppThemeService
{
    private const double AccentGradientHighlight = 0.25;
    private const double AccentGradientMiddleOffset = 0.55;
    private const double AccentGradientShadow = 0.15;
    private const double GlassAccentTopLightening = 0.80;
    private const double GlassAccentBottomLightening = 0.90;
    private const byte GlassAccentTopOpacity = 0xCC;
    private const byte GlassAccentBottomOpacity = 0xB8;

    private static ResourceDictionary BuildAccentOverlay(Color accent, bool isDark)
    {
        var overlay = new ResourceDictionary { [AccentOverlayMarkerKey] = true };
        var colors = AccentPalette.BuildColorOverrides(accent, isDark);
        foreach (var (key, color) in colors)
        {
            overlay[key] = color;
        }

        overlay["BrushPrimaryBright"] = CreateBrush(colors["ColorPrimaryBright"]);
        overlay["BrushPrimarySoft"] = CreateBrush(colors["ColorPrimarySoft"]);
        overlay["BrushAccentSoft"] = CreateBrush(colors["ColorAccentSoft"]);
        overlay["BrushFocusRing"] = CreateBrush(colors["ColorFocusRing"]);
        overlay["BrushControlSelected"] = CreateBrush(colors["ColorControlSelected"]);
        overlay["BrushListSelectedHover"] = CreateBrush(colors["ColorListSelectedHover"]);
        overlay["BrushPrimarySolid"] = CreateBrush(colors["ColorPrimaryFill"]);
        overlay["BrushPrimarySolidHover"] = CreateBrush(colors["ColorPrimaryFillHover"]);
        overlay["BrushPrimarySolidPressed"] = CreateBrush(colors["ColorPrimaryFillPressed"]);
        overlay["BrushPrimaryGradient"] = CreateAccentGradient(accent);
        overlay["BrushGlassAccent"] = CreateGlassAccent(accent, isDark, colors);
        return overlay;
    }

    private static LinearGradientBrush CreateAccentGradient(Color accent)
    {
        var gradient = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(0, 1) };
        gradient.GradientStops.Add(new GradientStop(AccentPalette.Lighten(accent, AccentGradientHighlight), 0));
        gradient.GradientStops.Add(new GradientStop(accent, AccentGradientMiddleOffset));
        gradient.GradientStops.Add(new GradientStop(AccentPalette.Darken(accent, AccentGradientShadow), 1));
        gradient.Freeze();
        return gradient;
    }

    private static LinearGradientBrush CreateGlassAccent(
        Color accent, bool isDark, IReadOnlyDictionary<string, Color> colors)
    {
        var glassAccent = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 1) };
        if (isDark)
        {
            glassAccent.GradientStops.Add(new GradientStop(colors["ColorAccentSoft"], 0));
            glassAccent.GradientStops.Add(new GradientStop(colors["ColorControlSelected"], 1));
        }
        else
        {
            glassAccent.GradientStops.Add(new GradientStop(
                AccentPalette.WithAlpha(AccentPalette.Lighten(accent, GlassAccentTopLightening), GlassAccentTopOpacity), 0));
            glassAccent.GradientStops.Add(new GradientStop(
                AccentPalette.WithAlpha(AccentPalette.Lighten(accent, GlassAccentBottomLightening), GlassAccentBottomOpacity), 1));
        }
        glassAccent.Freeze();
        return glassAccent;
    }

    private static SolidColorBrush CreateBrush(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}
