using Xunit;

namespace GestureClip.Tests.Gestures;

public sealed partial class GestureOverlayWindowTests
{
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
        var xaml = ReadHudXaml();
        Assert.Contains("StrokeThickness=\"2.6\"", xaml);
        Assert.DoesNotContain("StrokeThickness=\"6\"", xaml);
    }

    [Fact]
    public void GestureOverlayWindow_uses_open_polyline_trace_not_closed_shape()
    {
        var path = FindRepositoryFile("src", "GestureClip.App", "GestureOverlayWindow.xaml");
        var xaml = ReadHudXaml();
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
    public void GestureOverlayService_uses_dpi_aware_screen_coordinate_conversion()
    {
        var path = FindRepositoryFile("src", "GestureClip.App", "Services", "GestureOverlayService.cs");
        var source = File.ReadAllText(path);
        Assert.Contains("_window.PointFromScreen", source);
        Assert.Contains("physical screen pixels", source);
        Assert.DoesNotContain("point.X - originX", source);
        Assert.DoesNotContain("point.Y - originY", source);
    }

    [Fact]
    public void GestureOverlayService_throttles_trace_updates_and_backgrounds_workstation_snapshot()
    {
        var path = FindRepositoryFile("src", "GestureClip.App", "Services", "GestureOverlayService.cs");
        var source = File.ReadAllText(path);
        Assert.Contains("HudFrameIntervalMs", source);
        Assert.Contains("QueuePendingUpdateAsync", source);
        Assert.Contains("DispatcherPriority.Render", source);
        Assert.Contains("Volatile.Read(ref _generation)", source);
        Assert.Contains("Task.Run(async () =>", source);
        Assert.Contains("BuildSnapshotAsync", source);
        Assert.Contains("TotalMilliseconds < 750", source);
        Assert.Contains("gesture drawing must stay smooth", source);
        var applyStart = source.IndexOf("private void ApplyHudInfo", StringComparison.Ordinal);
        var applyEnd = source.IndexOf("private static bool IsUnboundHud", applyStart, StringComparison.Ordinal);
        var applyMethod = source[applyStart..applyEnd];
        Assert.DoesNotContain("QueueWorkstationSnapshotRefresh", applyMethod);
    }
}
