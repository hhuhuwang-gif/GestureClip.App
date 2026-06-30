using Xunit;

namespace GestureClip.Tests.Gestures;

public sealed class GestureOverlayWindowTests
{
    [Fact]
    public void GestureOverlayWindow_does_not_steal_focus_or_mouse_input()
    {
        var path = FindRepositoryFile("src", "GestureClip.App", "GestureOverlayWindow.xaml");

        var xaml = File.ReadAllText(path);

        Assert.Contains("ShowActivated=\"False\"", xaml);
        Assert.Contains("Focusable=\"False\"", xaml);
        Assert.Contains("IsHitTestVisible=\"False\"", xaml);
        Assert.Contains("ShowInTaskbar=\"False\"", xaml);
        Assert.Contains("Topmost=\"True\"", xaml);
    }

    [Fact]
    public void GestureOverlayWindow_places_pattern_badge_near_bottom_center()
    {
        var path = FindRepositoryFile("src", "GestureClip.App", "GestureOverlayWindow.xaml");

        var xaml = File.ReadAllText(path);

        Assert.Contains("VerticalAlignment=\"Bottom\"", xaml);
        Assert.Contains("HorizontalAlignment=\"Center\"", xaml);
        Assert.Contains("MinWidth=\"420\"", xaml);
        Assert.Contains("Style=\"{StaticResource GlassPanelStyle}\"", xaml);
        Assert.DoesNotContain("Canvas.Left=\"24\"", xaml);
        Assert.DoesNotContain("Canvas.Top=\"24\"", xaml);
    }

    [Fact]
    public void GestureOverlayWindow_shows_hud_fields_beyond_raw_pattern()
    {
        var path = FindRepositoryFile("src", "GestureClip.App", "GestureOverlayWindow.xaml");

        var xaml = File.ReadAllText(path);

        Assert.Contains("DirectionText", xaml);
        Assert.Contains("ActionName", xaml);
        Assert.Contains("ShortcutText", xaml);
        Assert.Contains("PresetName", xaml);
    }

    [Fact]
    public void GestureOverlayService_converts_screen_points_to_overlay_coordinates()
    {
        var path = FindRepositoryFile("src", "GestureClip.App", "Services", "GestureOverlayService.cs");

        var source = File.ReadAllText(path);

        Assert.Contains("PointFromScreen", source);
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
