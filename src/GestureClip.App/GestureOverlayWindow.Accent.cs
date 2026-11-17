using System.Windows.Media;
using GestureClip.App.Services;
using Color = System.Windows.Media.Color;
using WpfPoint = System.Windows.Point;

namespace GestureClip.App;

/// <summary>
/// 手势 HUD 的方块流动层配色。默认（青→靛→紫→品红）写在 GestureOverlayWindow.xaml 里；
/// 用户选了强调色后，这里用强调色重新生成一组首尾透明、多段彩色、可无缝平移的渐变。
/// </summary>
public static class HudAccentFlow
{
    /// <summary>0x52 ≈ 32% 不透明：和 XAML 默认层的强度一致。</summary>
    private const byte BandAlpha = 0x52;

    private const byte PeakAlpha = 0x5A;

    private static readonly Color VioletAnchor = Color.FromRgb(0x73, 0x50, 0xD8);
    private static readonly Color MagentaAnchor = Color.FromRgb(0xAE, 0x44, 0x80);

    public static LinearGradientBrush CreatePrimary(Color accent) =>
        Build(accent, new WpfPoint(1, 0), new[]
        {
            (0d, (Color?)null),
            (0.05, Band(accent, BandAlpha)),
            (0.13, Blend(accent, VioletAnchor, 0.45, PeakAlpha)),
            (0.20, Blend(accent, MagentaAnchor, 0.35, PeakAlpha)),
            (0.27, Blend(accent, Lighten(accent, 0.55), 0.5, BandAlpha)),
            (0.34, (Color?)null),
            (1d, (Color?)null)
        });

    public static LinearGradientBrush CreateSecondary(Color accent) =>
        Build(accent, new WpfPoint(0.42, 1), new[]
        {
            (0d, (Color?)null),
            (0.50, (Color?)null),
            (0.63, Band(Lighten(accent, 0.22), PeakAlpha)),
            (0.77, Blend(accent, VioletAnchor, 0.5, PeakAlpha)),
            (0.93, (Color?)null),
            (1d, (Color?)null)
        });

    /// <summary>扫描光柱保持“白光”观感，只用强调色染一点点冷调。</summary>
    public static LinearGradientBrush CreateScanBeam(Color accent)
    {
        var highlight = Color.FromArgb(0x3A, (byte)(0xE8 - (0xE8 - accent.R) * 0.12),
            (byte)(0xFB - (0xFB - accent.G) * 0.12),
            (byte)(0xFF - (0xFF - accent.B) * 0.12));
        var halo = AccentPalette.WithAlpha(highlight, 0x1C);

        return Build(accent, new WpfPoint(1, 0), new[]
        {
            (0d, (Color?)null),
            (0.40, (Color?)null),
            (0.47, halo),
            (0.50, highlight),
            (0.53, halo),
            (0.60, (Color?)null),
            (1d, (Color?)null)
        });
    }

    private static LinearGradientBrush Build(Color accent, WpfPoint endPoint, (double Offset, Color? Color)[] stops)
    {
        var brush = new LinearGradientBrush
        {
            StartPoint = new WpfPoint(0, 0),
            EndPoint = endPoint,
            SpreadMethod = GradientSpreadMethod.Repeat,
            RelativeTransform = new TranslateTransform()
        };

        foreach (var (offset, color) in stops)
        {
            // 首尾都必须与“透明底色”同色，否则平移一个周期会看到接缝。
            brush.GradientStops.Add(new GradientStop(color ?? Band(accent, 0), offset));
        }

        // 不 Freeze：RelativeTransform 要留给动画驱动，冻结后无法再动画。
        return brush;
    }

    private static Color Band(Color color, byte alpha) => AccentPalette.WithAlpha(color, alpha);

    private static Color Lighten(Color color, double amount) => AccentPalette.Lighten(color, amount);

    private static Color Blend(Color from, Color to, double amount, byte alpha) =>
        AccentPalette.WithAlpha(AccentPalette.Blend(from, to, amount), alpha);
}
