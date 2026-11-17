using System.Windows;
using System.Windows.Media;
using GestureClip.App;
using Xunit;

namespace GestureClip.Tests.Gestures;

/// <summary>
/// HUD 方块流动层跟随强调色时必须保持：首尾同色（无缝平移）、有可动画的平移变换、
/// 至少四种颜色（否则看不出渐变）。
/// </summary>
public sealed class HudAccentFlowTests
{
    private static readonly Color Accent = Color.FromRgb(0xE1, 0x1D, 0x48);

    public static TheoryData<string> Builders => new() { "primary", "secondary", "scan" };

    private static LinearGradientBrush Create(string kind) => kind switch
    {
        "primary" => HudAccentFlow.CreatePrimary(Accent),
        "secondary" => HudAccentFlow.CreateSecondary(Accent),
        _ => HudAccentFlow.CreateScanBeam(Accent)
    };

    [Theory]
    [MemberData(nameof(Builders))]
    public void Flow_brushes_loop_without_a_seam(string kind)
    {
        var brush = Create(kind);
        Assert.Equal(GradientSpreadMethod.Repeat, brush.SpreadMethod);

        var stops = brush.GradientStops;
        Assert.True(stops.Count >= 4, $"{kind} needs at least four stops.");
        Assert.Equal(stops[0].Color, stops[^1].Color);

        // 首尾必须是同名透明色，否则 Repeat 平移一个周期会露出色块。
        Assert.Equal(0, stops[0].Color.A);
        Assert.Equal(0, stops[^1].Color.A);
        Assert.Equal(stops[0].Color.R, stops[^1].Color.R);
    }

    [Theory]
    [MemberData(nameof(Builders))]
    public void Flow_brushes_expose_a_translatable_transform(string kind)
    {
        var brush = Create(kind);
        var transform = Assert.IsType<TranslateTransform>(brush.RelativeTransform);
        Assert.False(transform.IsFrozen, $"{kind} transform must stay animatable.");
    }

    [Fact]
    public void Primary_band_is_anchored_on_the_requested_accent()
    {
        var brush = HudAccentFlow.CreatePrimary(Accent);
        var band = brush.GradientStops.Single(stop => Math.Abs(stop.Offset - 0.05) < 0.0001);

        Assert.Equal(Accent.R, band.Color.R);
        Assert.Equal(Accent.G, band.Color.G);
        Assert.Equal(Accent.B, band.Color.B);
        Assert.True(band.Color.A > 0x30, "The accent band must be visible.");
    }

    [Fact]
    public void Secondary_stream_runs_diagonally()
    {
        var brush = HudAccentFlow.CreateSecondary(Accent);

        Assert.Equal(new Point(0, 0), brush.StartPoint);
        Assert.Equal(new Point(0.42, 1), brush.EndPoint);
    }

    [Fact]
    public void Scan_beam_keeps_a_bright_peak_in_the_middle()
    {
        var brush = HudAccentFlow.CreateScanBeam(Accent);
        var peak = brush.GradientStops.Single(stop => Math.Abs(stop.Offset - 0.5) < 0.0001);

        Assert.Equal(0x3A, peak.Color.A);
    }

    [Fact]
    public void Different_accents_produce_different_gradients()
    {
        var red = HudAccentFlow.CreatePrimary(Color.FromRgb(0xE1, 0x1D, 0x48));
        var green = HudAccentFlow.CreatePrimary(Color.FromRgb(0x10, 0xB9, 0x81));

        var redBand = red.GradientStops.Single(stop => Math.Abs(stop.Offset - 0.05) < 0.0001).Color;
        var greenBand = green.GradientStops.Single(stop => Math.Abs(stop.Offset - 0.05) < 0.0001).Color;

        Assert.NotEqual(redBand, greenBand);
    }
}
