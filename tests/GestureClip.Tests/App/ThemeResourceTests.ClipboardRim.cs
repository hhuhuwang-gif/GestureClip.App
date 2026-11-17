using Xunit;
using GestureClip.Tests.TestDoubles;

namespace GestureClip.Tests.App;

public sealed partial class ThemeResourceTests
{
    /// <summary>
    /// 深色浮层统一用受光边（顶部亮、底部隐没），与设置页浅色卡片是同一套设计语言。
    /// 这里锁死，避免后续有人手改回均匀描边。
    /// </summary>
    [Fact]
    public void ClipboardOverlay_surfaces_share_the_lit_dark_rim()
    {
        var xamlPath = FindRepositoryFile("src", "GestureClip.App", "ClipboardOverlayWindow.xaml");
        var xaml = File.ReadAllText(xamlPath);

        var rimCount = System.Text.RegularExpressions.Regex.Matches(
            xaml, "BorderBrush=\"\\{DynamicResource BrushCardRimDark\\}\"").Count;
        Assert.True(rimCount >= 8, $"Expected every dark overlay surface to use the lit rim, found {rimCount}.");

        var brushesPath = FindRepositoryFile("src", "GestureClip.App", "Themes", "Brushes.xaml");
        var brushes = File.ReadAllText(brushesPath);
        Assert.Contains("x:Key=\"BrushCardRimDark\"", brushes);
        Assert.Contains("LinearGradientBrush", brushes);
    }
}
