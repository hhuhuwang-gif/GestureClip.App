using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using GestureClip.Core.Gestures;

namespace GestureClip.App.Services;

public sealed partial class GestureOverlayService
{
    private System.Windows.Media.Brush GetStrokeBrush(string colorText)
    {
        if (_lastStrokeBrush is not null &&
            string.Equals(_lastStrokeColorText, colorText, StringComparison.OrdinalIgnoreCase))
        {
            return _lastStrokeBrush;
        }

        _lastStrokeColorText = colorText;
        _lastStrokeBrush = CreateStrokeBrush(colorText);
        return _lastStrokeBrush;
    }

    private static System.Windows.Media.Brush CreateStrokeBrush(string colorText)
    {
        try
        {
            var brush = (SolidColorBrush)new BrushConverter().ConvertFromString(colorText)!;
            brush.Freeze();
            return brush;
        }
        catch
        {
            var fallback = new SolidColorBrush(System.Windows.Media.Color.FromRgb(140, 200, 255));
            fallback.Freeze();
            return fallback;
        }
    }

    private System.Windows.Media.Brush GetHudBackgroundBrush(string themeKey, string startColor, string endColor)
    {
        if (_lastHudBackgroundBrush is not null &&
            string.Equals(_lastHudThemeKey, themeKey, StringComparison.OrdinalIgnoreCase))
        {
            return _lastHudBackgroundBrush;
        }

        _lastHudThemeKey = themeKey;
        _lastHudBackgroundBrush = CreateHudGradientBrush(startColor, endColor);
        return _lastHudBackgroundBrush;
    }

    private System.Windows.Media.Brush GetHudAccentBrush(string colorText)
    {
        if (_lastHudAccentBrush is not null &&
            string.Equals(_lastHudAccentColorText, colorText, StringComparison.OrdinalIgnoreCase))
        {
            return _lastHudAccentBrush;
        }

        _lastHudAccentColorText = colorText;
        _lastHudAccentBrush = CreateSolidBrush(colorText, System.Windows.Media.Color.FromRgb(147, 197, 253));
        return _lastHudAccentBrush;
    }

    private static System.Windows.Media.Brush CreateHudGradientBrush(string startColor, string endColor)
    {
        var start = ParseColor(startColor, System.Windows.Media.Color.FromArgb(220, 17, 23, 36));
        var end = ParseColor(endColor, System.Windows.Media.Color.FromArgb(220, 30, 41, 59));
        var brush = new LinearGradientBrush(start, end, 0)
        {
            StartPoint = new System.Windows.Point(0, 0),
            EndPoint = new System.Windows.Point(1, 1)
        };
        brush.Freeze();
        return brush;
    }

    private static System.Windows.Media.Brush CreateSolidBrush(string colorText, System.Windows.Media.Color fallbackColor)
    {
        var brush = new SolidColorBrush(ParseColor(colorText, fallbackColor));
        brush.Freeze();
        return brush;
    }

    private static System.Windows.Media.Color ParseColor(string colorText, System.Windows.Media.Color fallback)
    {
        try
        {
            return (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(colorText);
        }
        catch
        {
            return fallback;
        }
    }

}
