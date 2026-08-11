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

        Assert.Contains("MinWidth=\"380\"", xaml);

        Assert.Contains("MaxWidth=\"520\"", xaml);

        Assert.Contains("MinHeight=\"104\"", xaml);

        Assert.Contains("BorderThickness=\"1\"", xaml);

        Assert.Contains("CornerRadius=\"18\"", xaml);

        Assert.DoesNotContain("Canvas.Left=\"24\"", xaml);

        Assert.DoesNotContain("Canvas.Top=\"24\"", xaml);

    }



    [Fact]

    public void GestureOverlayWindow_uses_clean_compact_glass_hud_layout()

    {

        var path = FindRepositoryFile("src", "GestureClip.App", "GestureOverlayWindow.xaml");



        var xaml = File.ReadAllText(path);



        Assert.Contains("CompactGameHud", xaml);

        Assert.Contains("DirectionText", xaml);

        Assert.Contains("ActionName", xaml);

        Assert.Contains("ShortcutText", xaml);

        Assert.Contains("WorkStatusText", xaml);

        Assert.Contains("HudCardBrush", xaml);

        Assert.DoesNotContain("MinHeight=\"200\"", xaml);

        Assert.DoesNotContain("MaxHeight=", xaml);

        Assert.DoesNotContain("GESTURE HUD", xaml);

        Assert.DoesNotContain("MiniStatusMetricsRow", xaml);

        Assert.DoesNotContain("UniformGrid", xaml);

        Assert.DoesNotContain("PIXEL STATUS", xaml);

        Assert.DoesNotContain("TodayStatsLine", xaml);

        Assert.DoesNotContain("GestureActionStrip", xaml);

    }



    [Fact]

    public void GestureOverlayWindow_shows_hud_fields_beyond_raw_pattern()

    {

        var path = FindRepositoryFile("src", "GestureClip.App", "GestureOverlayWindow.xaml");



        var xaml = File.ReadAllText(path);



        Assert.Contains("DirectionText", xaml);

        Assert.Contains("ActionName", xaml);

        Assert.Contains("ShortcutText", xaml);

        Assert.Contains("WorkStatusText", xaml);

        Assert.DoesNotContain("PresetName", xaml);

    }



    [Fact]

    public void GestureOverlayWindow_shows_compact_workstation_context()

    {

        var path = FindRepositoryFile("src", "GestureClip.App", "GestureOverlayWindow.xaml");



        var xaml = File.ReadAllText(path);



        Assert.Contains("WorkStatusText", xaml);

        Assert.Contains("HudAccentBrush", xaml);

        Assert.Contains("Microsoft YaHei UI", xaml);

        Assert.Contains("SnapsToDevicePixels=\"True\"", xaml);

        Assert.DoesNotContain("GestureCountText", xaml);

        Assert.DoesNotContain("CopyCountText", xaml);

        Assert.DoesNotContain("PasteCountText", xaml);

        Assert.DoesNotContain("SavedClicksCountText", xaml);

        Assert.DoesNotContain("WorkSummaryText", xaml);

        Assert.DoesNotContain("TodayFishingValueText", xaml);

        Assert.DoesNotContain("FunText", xaml);

    }



    [Fact]

    public void GestureOverlayWindow_avoids_clipped_direction_text()

    {

        var path = FindRepositoryFile("src", "GestureClip.App", "GestureOverlayWindow.xaml");



        var xaml = File.ReadAllText(path);



        Assert.DoesNotContain("按住右键拖动", xaml);

        Assert.DoesNotContain("Text=\"按...\"", xaml);

        Assert.DoesNotContain("MaxHeight=", xaml);

        Assert.Contains("DirectionText", xaml);

        Assert.Contains("TextTrimming=\"CharacterEllipsis\"", xaml);

        Assert.Contains("MinHeight=\"104\"", xaml);

    }



    [Fact]

    public void GestureOverlayWindow_reserves_enough_vertical_space_for_direction_and_status()

    {

        var path = FindRepositoryFile("src", "GestureClip.App", "GestureOverlayWindow.xaml");



        var xaml = File.ReadAllText(path);



        Assert.Contains("MinHeight=\"104\"", xaml);

        Assert.Contains("Width=\"56\"", xaml);

        Assert.Contains("Height=\"56\"", xaml);

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

        Assert.Contains("WorkSummaryText", viewModel);

        Assert.Contains("BuildSnapshotAsync", service);

    }



    [Fact]

    public void GestureOverlayWindow_uses_thin_gesture_trace()

    {

        var path = FindRepositoryFile("src", "GestureClip.App", "GestureOverlayWindow.xaml");



        var xaml = File.ReadAllText(path);



        Assert.Contains("StrokeThickness=\"2.6\"", xaml);

        Assert.DoesNotContain("StrokeThickness=\"6\"", xaml);

    }



    [Fact]

    public void GestureOverlayWindow_uses_open_polyline_trace_not_closed_shape()

    {

        var path = FindRepositoryFile("src", "GestureClip.App", "GestureOverlayWindow.xaml");



        var xaml = File.ReadAllText(path);



        Assert.Contains("<Polyline", xaml);

        Assert.DoesNotContain("<Polygon", xaml);

        Assert.DoesNotContain("PathGeometry", xaml);

        Assert.DoesNotContain("Fill=\"{Binding StrokeBrush}\"", xaml);

    }



    [Fact]

    public void GestureOverlayService_caps_visible_trace_points_without_readding_start()

    {

        var path = FindRepositoryFile("src", "GestureClip.App", "Services", "GestureOverlayService.cs");



        var source = File.ReadAllText(path);



        Assert.Contains("MaxVisiblePointCount = 96", source);

        Assert.Contains("points.Skip(points.Count - MaxVisiblePointCount).ToArray()", source);

        Assert.DoesNotContain("Prepend(points[0])", source);

    }



    [Fact]

    public void GestureOverlayService_fades_overlay_before_hiding()

    {

        var path = FindRepositoryFile("src", "GestureClip.App", "Services", "GestureOverlayService.cs");



        var source = File.ReadAllText(path);



        Assert.Contains("DoubleAnimation", source);

        Assert.Contains("OpacityProperty", source);

        Assert.Contains("HideFadeMilliseconds", source);

    }



    [Fact]

    public void GestureOverlayService_converts_screen_points_to_overlay_coordinates()

    {

        var path = FindRepositoryFile("src", "GestureClip.App", "Services", "GestureOverlayService.cs");



        var source = File.ReadAllText(path);



        Assert.Contains("PointFromScreen", source);

    }



    [Fact]

    public void GestureOverlayService_throttles_trace_updates_and_backgrounds_workstation_snapshot()

    {

        var path = FindRepositoryFile("src", "GestureClip.App", "Services", "GestureOverlayService.cs");



        var source = File.ReadAllText(path);



        Assert.Contains("TotalMilliseconds < 33", source);

        Assert.Contains("StorePendingUpdate(points, hudInfo)", source);

        Assert.Contains("Task.Run(async () =>", source);

        Assert.Contains("BuildSnapshotAsync", source);

        Assert.Contains("TotalMilliseconds < 750", source);

        Assert.Contains("gesture drawing must stay smooth", source);

    }



    [Fact]

    public void GestureOverlayWindow_keeps_fun_report_level_fields_off_canvas()

    {

        var path = FindRepositoryFile("src", "GestureClip.App", "GestureOverlayWindow.xaml");

        var viewModelPath = FindRepositoryFile("src", "GestureClip.App", "ViewModels", "GestureOverlayViewModel.cs");



        var xaml = File.ReadAllText(path);

        var viewModel = File.ReadAllText(viewModelPath);



        Assert.DoesNotContain("FunText", xaml);

        Assert.DoesNotContain("GainedXpText", xaml);

        Assert.DoesNotContain("LevelText", xaml);

        Assert.Contains("FunText", viewModel);

        Assert.Contains("LevelText", viewModel);

        Assert.Contains("XpProgressPercent", viewModel);

    }


    [Fact]
    public void GestureOverlayWindow_draws_only_never_intercepts_mouse_and_hides_on_fixed_delay()
    {
        var path = FindRepositoryFile("src", "GestureClip.App", "GestureOverlayWindow.xaml");
        var xaml = File.ReadAllText(path);
        var vm = File.ReadAllText(FindRepositoryFile("src", "GestureClip.App", "ViewModels", "GestureOverlayViewModel.cs"));
        var overlay = File.ReadAllText(FindRepositoryFile("src", "GestureClip.App", "Services", "GestureOverlayService.cs"));
        var mouse = File.ReadAllText(FindRepositoryFile("src", "GestureClip.Features", "Gestures", "MouseGestureService.cs"));

        // Bind CTA was removed in P1: HUD is draw-only and must never intercept input.
        Assert.DoesNotContain("BindActionBar", xaml);
        Assert.DoesNotContain("ShowBindAction", xaml);
        Assert.DoesNotContain("BindUnboundGestureCommand", xaml);
        Assert.DoesNotContain("去绑定", vm);
        Assert.DoesNotContain("_holdForBindAction", overlay);
        Assert.DoesNotContain("4500", overlay);
        Assert.Contains("IsHitTestVisible = false", overlay);
        Assert.Contains("HideDelayMilliseconds", overlay);
        Assert.Contains("resolvedAction != BuiltInGestureAction.None", mouse);
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













