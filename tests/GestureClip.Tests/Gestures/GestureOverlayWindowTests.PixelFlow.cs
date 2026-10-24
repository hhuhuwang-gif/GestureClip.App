using System.Xml.Linq;
using Xunit;

namespace GestureClip.Tests.Gestures;

public sealed partial class GestureOverlayWindowTests
{
    [Fact]
    public void GestureOverlayWindow_layers_digital_pixel_flow_above_the_surface_wash()
    {
        var window = LoadHudDocuments()[0];
        var panel = FindNamedElement(window, "CompactGameHud");
        var order = panel.Descendants().ToList();

        var wash = FindNamedElement(window, "HudThemeWash");
        var grid = FindNamedElement(window, "HudPixelGrid");
        var scrim = FindNamedElement(window, "HudCenterScrim");
        var flow = FindNamedElement(window, "HudPixelFlow");
        var alt = FindNamedElement(window, "HudPixelFlowAlt");
        var beam = FindNamedElement(window, "HudScanBeam");

        Assert.Equal("{StaticResource HudPixelGridBrush}", (string?)grid.Attribute("Background"));
        Assert.Equal("{StaticResource HudPixelMaskBrush}", (string?)flow.Attribute("OpacityMask"));
        Assert.Equal("{StaticResource HudPixelMaskBrush}", (string?)alt.Attribute("OpacityMask"));
        Assert.Contains(scrim, panel.Descendants());

        // 层序固定：主题色 → 点阵 → 收束雾 → 主流 → 副流 → 扫描光
        Assert.True(order.IndexOf(wash) < order.IndexOf(grid));
        Assert.True(order.IndexOf(grid) < order.IndexOf(scrim));
        Assert.True(order.IndexOf(scrim) < order.IndexOf(flow));
        Assert.True(order.IndexOf(flow) < order.IndexOf(alt));
        Assert.True(order.IndexOf(alt) < order.IndexOf(beam));
    }

    [Fact]
    public void GestureOverlayWindow_flow_uses_repeating_translated_gradient_so_it_can_loop()
    {
        var window = LoadHudDocuments()[0];
        foreach (var name in new[] { "HudPixelFlow", "HudPixelFlowAlt", "HudScanBeam" })
        {
            var element = FindNamedElement(window, name);
            var background = element.Elements().Single(child => child.Name.LocalName == "Border.Background");
            var brush = background.Elements().Single();
            Assert.Equal("LinearGradientBrush", brush.Name.LocalName);
            Assert.Equal("Repeat", (string?)brush.Attribute("SpreadMethod"));
            Assert.Single(brush.Descendants(), node => node.Name.LocalName == "TranslateTransform");

            // 首尾同色 —— 这样平移一个周期后接缝不可见
            var stops = brush.Elements().Where(node => node.Name.LocalName == "GradientStop").ToArray();
            Assert.True(stops.Length >= 4);
            Assert.Equal((string?)stops[0].Attribute("Color"), (string?)stops[^1].Attribute("Color"));
            Assert.All(stops, stop => Assert.NotNull(stop.Attribute("Offset")));
        }
    }

    [Fact]
    public void GestureOverlayWindow_pixel_brushes_are_tiling_drawing_brushes()
    {
        var documents = LoadHudDocuments();
        foreach (var key in new[]
                 {
                     "HudPixelGridBrush", "HudPixelMaskBrush",
                     "HudSegmentTrackBrush", "HudSegmentMaskBrush"
                 })
        {
            var brush = documents.SelectMany(document => document.Descendants()).Single(element =>
                (string?)element.Attribute(XamlNamespace + "Key") == key);
            Assert.Equal("DrawingBrush", brush.Name.LocalName);
            Assert.Equal("Tile", (string?)brush.Attribute("TileMode"));
            Assert.Equal("Absolute", (string?)brush.Attribute("ViewportUnits"));
            Assert.NotNull(brush.Descendants().SingleOrDefault(node => node.Name.LocalName == "RectangleGeometry"));
        }
    }

    [Fact]
    public void GestureOverlayWindow_xp_bar_renders_as_segmented_led_strip()
    {
        var style = FindStyle(LoadHudDocuments(), "HudXpProgress");
        var indicator = FindNamedElement(style, "PART_Indicator");
        Assert.Equal("{StaticResource HudSegmentMaskBrush}", (string?)indicator.Attribute("OpacityMask"));
        Assert.Equal("{TemplateBinding Foreground}", (string?)indicator.Attribute("Background"));

        var track = FindNamedElement(style, "PART_Track");
        Assert.Equal("{StaticResource HudSegmentTrackBrush}", (string?)track.Attribute("Background"));
    }

    [Fact]
    public void GestureOverlayWindow_headline_keeps_accent_focus_and_frame_details()
    {
        var window = LoadHudDocuments()[0];
        var documents = LoadHudDocuments();

        // 标题前的 2x2 渐变色块图标
        var icon = FindNamedElement(window, "HudMarkIcon");
        var markGrid = icon.Elements().Single(element => element.Name.LocalName == "Grid.RowDefinitions");
        Assert.Equal(2, markGrid.Elements().Count());
        Assert.Equal(4, icon.Elements().Count(element => element.Name.LocalName == "Border"));
        Assert.Equal(4, icon.Elements()
            .Where(element => element.Name.LocalName == "Border")
            .Select(element => (string?)element.Attribute("Background"))
            .Distinct()
            .Count());

        // 标题下 1px 渐变分隔线 + 顶部镜面高光
        var divider = documents.SelectMany(document => document.Descendants()).Single(element =>
            (string?)element.Attribute(XamlNamespace + "Key") == "HudDividerBrush");
        Assert.Equal("LinearGradientBrush", divider.Name.LocalName);
        Assert.NotNull(window.Descendants().SingleOrDefault(element =>
            (string?)element.Attribute("Background") == "{StaticResource HudTopHighlightBrush}"));
    }
}
