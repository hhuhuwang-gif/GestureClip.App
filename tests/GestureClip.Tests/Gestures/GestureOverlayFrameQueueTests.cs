using System.Reflection;
using GestureClip.App.Services;
using GestureClip.Core.Gestures;
using Xunit;

namespace GestureClip.Tests.Gestures;

public sealed class GestureOverlayFrameQueueTests
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    [Fact]
    public void Previous_gesture_frame_does_not_consume_current_gesture_points()
    {
        var service = new GestureOverlayService(null!, null!, null!);
        IReadOnlyList<GesturePoint> points = [new(30, 40, DateTimeOffset.UtcNow)];
        SetField(service, "_generation", 2);
        SetField(service, "_pendingUpdatePoints", points);
        SetField(service, "_pendingUpdateHudInfo", new GestureHudInfo("<", "<", "录制", "", ""));
        SetField(service, "_updateQueued", true);

        typeof(GestureOverlayService).GetMethod("FlushPendingUpdate", PrivateInstance)!
            .Invoke(service, [1]);

        Assert.Same(points, GetField(service, "_pendingUpdatePoints"));
        Assert.Equal(true, GetField(service, "_updateQueued"));
    }

    [Fact]
    public void Previous_gesture_failure_does_not_clear_current_pending_frame()
    {
        var service = new GestureOverlayService(null!, null!, null!);
        IReadOnlyList<GesturePoint> points = [new(30, 40, DateTimeOffset.UtcNow)];
        SetField(service, "_generation", 2);
        SetField(service, "_pendingUpdatePoints", points);
        SetField(service, "_updateQueued", true);

        typeof(GestureOverlayService).GetMethod("ClearPendingUpdate", PrivateInstance)!
            .Invoke(service, [1]);

        Assert.Same(points, GetField(service, "_pendingUpdatePoints"));
        Assert.Equal(true, GetField(service, "_updateQueued"));
    }

    [Fact]
    public void New_presentation_invalidates_previous_frame_before_dispatch()
    {
        var service = new GestureOverlayService(null!, null!, null!);
        SetField(service, "_generation", 2);
        SetField(service, "_pendingUpdatePoints", new[] { new GesturePoint(30, 40, DateTimeOffset.UtcNow) });
        SetField(service, "_updateQueued", true);

        var generation = typeof(GestureOverlayService).GetMethod("BeginPresentation", PrivateInstance)!
            .Invoke(service, null);

        Assert.Equal(3, generation);
        Assert.Null(GetField(service, "_pendingUpdatePoints"));
        Assert.Equal(false, GetField(service, "_updateQueued"));
    }

    private static void SetField(GestureOverlayService service, string name, object value) =>
        typeof(GestureOverlayService).GetField(name, PrivateInstance)!.SetValue(service, value);

    private static object? GetField(GestureOverlayService service, string name) =>
        typeof(GestureOverlayService).GetField(name, PrivateInstance)!.GetValue(service);
}
