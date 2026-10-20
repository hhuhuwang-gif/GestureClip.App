using Xunit;

namespace GestureClip.Tests.Gestures;

public sealed partial class GestureOverlayWindowTests
{
    [Fact]
    public void GestureOverlayWindow_does_not_steal_focus_or_mouse_input()
    {
        var path = FindRepositoryFile("src", "GestureClip.App", "GestureOverlayWindow.xaml");
        var xaml = ReadHudXaml();
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
        var xaml = ReadHudXaml();
        Assert.Contains("VerticalAlignment=\"Bottom\"", xaml);
        Assert.Contains("HorizontalAlignment=\"Center\"", xaml);
        Assert.Contains("MinWidth=\"720\"", xaml);
        Assert.Contains("MinHeight=\"200\"", xaml);
        Assert.Contains("BorderThickness=\"1\"", xaml);
        Assert.Contains("CornerRadius=\"18\"", xaml);
        Assert.DoesNotContain("Canvas.Left=\"24\"", xaml);
        Assert.DoesNotContain("Canvas.Top=\"24\"", xaml);
    }

    [Fact]
    public void GestureOverlayWindow_uses_clean_compact_glass_hud_layout()
    {
        var path = FindRepositoryFile("src", "GestureClip.App", "GestureOverlayWindow.xaml");
        var xaml = ReadHudXaml();
        Assert.Contains("CompactGameHud", xaml);
        Assert.Contains("GestureActionStrip", xaml);
        Assert.Contains("TodayStatsLine", xaml);
        Assert.Contains("松开右键执行", xaml);
        AssertNormalRightClickNeedsMovement();
        Assert.Contains("MinHeight=\"200\"", xaml);
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
        var xaml = ReadHudXaml();
        Assert.Contains("DirectionText", xaml);
        Assert.Contains("ActionName", xaml);
        Assert.Contains("ShortcutText", xaml);
        Assert.Contains("PresetName", xaml);
    }

    [Fact]
    public void GestureOverlayWindow_shows_compact_workstation_context()
    {
        var path = FindRepositoryFile("src", "GestureClip.App", "GestureOverlayWindow.xaml");
        var xaml = ReadHudXaml();
        Assert.Contains("工位小熊", xaml);
        Assert.Contains("WorkStatusText", xaml);
        Assert.Contains("GestureCountText", xaml);
        Assert.Contains("CopyCountText", xaml);
        Assert.Contains("PasteCountText", xaml);
        Assert.Contains("SavedClicksCountText", xaml);
        Assert.Contains("Microsoft YaHei UI", xaml);
        Assert.Contains("WorkSummaryText", xaml);
        Assert.Contains("SnapsToDevicePixels=\"True\"", xaml);
        Assert.Contains("WorkSummaryText", xaml);
        Assert.Contains("FunText", xaml);
        Assert.Contains("LevelText", xaml);
        Assert.Contains("XpText", xaml);
        Assert.Contains("GainedXpText", xaml);
        Assert.Contains("GestureCountText", xaml);
        Assert.DoesNotContain("TodayFishingValueText", xaml);
        Assert.Contains("WorkSummaryText", xaml);
        Assert.Contains("GestureCountText", xaml);
    }

    [Fact]
    public void GestureOverlayWindow_avoids_clipped_direction_text()
    {
        var path = FindRepositoryFile("src", "GestureClip.App", "GestureOverlayWindow.xaml");
        var xaml = ReadHudXaml();
        Assert.DoesNotContain("按住右键拖动", xaml);
        Assert.DoesNotContain("Text=\"按...\"", xaml);
        Assert.DoesNotContain("MaxHeight=", xaml);
        Assert.Contains("MinWidth=\"720\"", xaml);
        Assert.Contains("MinHeight=\"200\"", xaml);
    }

    [Fact]
    public void GestureOverlayWindow_reserves_enough_vertical_space_for_workstation_cards()
    {
        var path = FindRepositoryFile("src", "GestureClip.App", "GestureOverlayWindow.xaml");
        var xaml = ReadHudXaml();
        Assert.Contains("MinHeight=\"200\"", xaml);
        Assert.Contains("MinWidth=\"720\"", xaml);
        Assert.DoesNotContain("Height=\"150\"", xaml);
    }

    [Fact]
    public void GestureOverlayWindow_shows_fun_report_level_and_xp()
    {
        var path = FindRepositoryFile("src", "GestureClip.App", "GestureOverlayWindow.xaml");
        var viewModelPath = FindRepositoryFile("src", "GestureClip.App", "ViewModels", "GestureOverlayViewModel.cs");
        var xaml = ReadHudXaml();
        var viewModel = File.ReadAllText(viewModelPath);
        Assert.Contains("FunText", xaml);
        Assert.Contains("GainedXpText", xaml);
        Assert.Contains("LevelText", xaml);
        Assert.Contains("XpText", xaml);
        Assert.Contains("WorkSummaryText", xaml);
        Assert.Contains("GestureCountText", xaml);
        Assert.Contains("FunText", viewModel);
        Assert.Contains("LevelText", viewModel);
        Assert.Contains("XpProgressPercent", viewModel);
        Assert.Contains("ProgressBar", xaml);
        AssertXpProgressStyleRange();
        Assert.Contains("Value=\"{Binding XpProgressPercent}\"", xaml);
    }

    [Fact]
    public void GestureOverlayWindow_removes_unbound_quick_bind_interaction()
    {
        var xaml = File.ReadAllText(FindRepositoryFile("src", "GestureClip.App", "GestureOverlayWindow.xaml"));
        var window = File.ReadAllText(FindRepositoryFile("src", "GestureClip.App", "GestureOverlayWindow.xaml.cs"));
        var vm = File.ReadAllText(FindRepositoryFile("src", "GestureClip.App", "ViewModels", "GestureOverlayViewModel.cs"));
        var overlay = File.ReadAllText(FindRepositoryFile("src", "GestureClip.App", "Services", "GestureOverlayService.cs"));
        var hud = File.ReadAllText(FindRepositoryFile("src", "GestureClip.Features", "Gestures", "GestureHudInfoProvider.cs"));
        Assert.DoesNotContain("BindActionBar", xaml);
        Assert.DoesNotContain("BindUnboundGestureCommand", xaml);
        Assert.DoesNotContain("BindActionBar_MouseLeftButtonUp", window);
        Assert.DoesNotContain("ShowBindAction", vm);
        Assert.DoesNotContain("NotifyBindActionChanged", vm);
        Assert.DoesNotContain("ShowGestureBindingEditor", vm);
        Assert.DoesNotContain("_holdForBindAction", overlay);
        Assert.DoesNotContain("4500", overlay);
        Assert.DoesNotContain("去绑定", hud);
        Assert.Contains("暂无动作", hud);
        Assert.Contains("Task.Delay(700", overlay);
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













