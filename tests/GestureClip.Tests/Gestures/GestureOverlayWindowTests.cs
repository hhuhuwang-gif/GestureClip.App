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
        Assert.Contains("Width=\"860\"", xaml);
        Assert.Contains("Height=\"178\"", xaml);
        Assert.Contains("BorderThickness=\"1\"", xaml);
        Assert.Contains("CornerRadius=\"24\"", xaml);
        Assert.DoesNotContain("Canvas.Left=\"24\"", xaml);
        Assert.DoesNotContain("Canvas.Top=\"24\"", xaml);
    }

    [Fact]
    public void GestureOverlayWindow_uses_clean_compact_glass_hud_layout()
    {
        var path = FindRepositoryFile("src", "GestureClip.App", "GestureOverlayWindow.xaml");

        var xaml = File.ReadAllText(path);

        Assert.Contains("CompactGameHud", xaml);
        Assert.Contains("GestureActionStrip", xaml);
        Assert.Contains("TodayStatsLine", xaml);
        Assert.Contains("松开右键执行", xaml);
        Assert.Contains("普通右键不显示", xaml);
        Assert.Contains("Height=\"178\"", xaml);
        Assert.DoesNotContain("MaxHeight=", xaml);
        Assert.DoesNotContain("GESTURE HUD", xaml);
        Assert.DoesNotContain("MiniStatusMetricsRow", xaml);
        Assert.DoesNotContain("UniformGrid", xaml);
        Assert.DoesNotContain("PIXEL STATUS", xaml);
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
    public void GestureOverlayWindow_shows_compact_workstation_context()
    {
        var path = FindRepositoryFile("src", "GestureClip.App", "GestureOverlayWindow.xaml");

        var xaml = File.ReadAllText(path);

        Assert.Contains("工位小熊", xaml);
        Assert.Contains("WorkStatusText", xaml);
        Assert.Contains("EfficiencyStatsText", xaml);
        Assert.Contains("SavedClicksText", xaml);
        Assert.Contains("SnapsToDevicePixels=\"True\"", xaml);
        Assert.Contains("今日工资", xaml);
        Assert.Contains("还有多久下班", xaml);
        Assert.Contains("还有多久发薪水", xaml);
        Assert.Contains("OffWorkCountdownText", xaml);
        Assert.Contains("TodayEarnedText", xaml);
        Assert.Contains("PaydayCountdownText", xaml);
        Assert.DoesNotContain("TodayFishingValueText", xaml);
    }

    [Fact]
    public void GestureOverlayWindow_avoids_clipped_direction_text()
    {
        var path = FindRepositoryFile("src", "GestureClip.App", "GestureOverlayWindow.xaml");

        var xaml = File.ReadAllText(path);

        Assert.DoesNotContain("按住右键拖动", xaml);
        Assert.DoesNotContain("Text=\"按...\"", xaml);
        Assert.DoesNotContain("MaxHeight=", xaml);
        Assert.Contains("Width=\"860\"", xaml);
        Assert.Contains("Height=\"178\"", xaml);
    }

    [Fact]
    public void GestureOverlayWindow_reserves_enough_vertical_space_for_workstation_cards()
    {
        var path = FindRepositoryFile("src", "GestureClip.App", "GestureOverlayWindow.xaml");

        var xaml = File.ReadAllText(path);

        Assert.Contains("Height=\"178\"", xaml);
        Assert.Contains("Width=\"860\"", xaml);
        Assert.DoesNotContain("Height=\"150\"", xaml);
    }

    [Fact]
    public void GestureOverlayService_updates_payday_countdown_for_hud()
    {
        var viewModelPath = FindRepositoryFile("src", "GestureClip.App", "ViewModels", "GestureOverlayViewModel.cs");
        var servicePath = FindRepositoryFile("src", "GestureClip.App", "Services", "GestureOverlayService.cs");

        var viewModel = File.ReadAllText(viewModelPath);
        var service = File.ReadAllText(servicePath);

        Assert.Contains("PaydayCountdownText", viewModel);
        Assert.Contains("snapshot.DaysUntilPayday", service);
        Assert.Contains("FormatPaydayCountdown", service);
    }

    [Fact]
    public void GestureOverlayWindow_uses_thin_gesture_trace()
    {
        var path = FindRepositoryFile("src", "GestureClip.App", "GestureOverlayWindow.xaml");

        var xaml = File.ReadAllText(path);

        Assert.Contains("StrokeThickness=\"3\"", xaml);
        Assert.DoesNotContain("StrokeThickness=\"6\"", xaml);
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
