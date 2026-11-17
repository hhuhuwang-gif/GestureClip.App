using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using GestureClip.Core.Gestures;

namespace GestureClip.App.Services;

public sealed partial class GestureOverlayService
{
    private async Task QueuePendingUpdateAsync(int generation)
    {
        try
        {
            var elapsedMs = (DateTimeOffset.UtcNow - _lastUpdateAt).TotalMilliseconds;
            var delayMs = Math.Max(0, HudFrameIntervalMs - (int)elapsedMs);
            if (delayMs > 0)
            {
                await Task.Delay(delayMs).ConfigureAwait(false);
            }

            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher is null)
            {
                ClearPendingUpdate(generation);
                return;
            }

            await dispatcher.InvokeAsync(
                () => FlushPendingUpdate(generation),
                DispatcherPriority.Render);
        }
        catch (Exception ex)
        {
            ClearPendingUpdate(generation);
            System.Diagnostics.Trace.TraceError("Gesture HUD frame update failed: {0}", ex);
        }
    }

    private void FlushPendingUpdate(int generation)
    {
#if DEBUG
        var hudWatch = System.Diagnostics.Stopwatch.StartNew();
#endif
        IReadOnlyList<GesturePoint>? points;
        GestureHudInfo? hudInfo;
        lock (_updateSyncRoot)
        {
            // A delayed frame from the previous gesture must not consume the new stroke.
            if (generation != _generation)
            {
                return;
            }

            points = _pendingUpdatePoints;
            hudInfo = _pendingUpdateHudInfo;
            _pendingUpdatePoints = null;
            _pendingUpdateHudInfo = null;
            _updateQueued = false;
        }

        if (points is null || hudInfo is null || generation != Volatile.Read(ref _generation))
        {
            return;
        }

        _lastUpdateAt = DateTimeOffset.UtcNow;
        EnsureWindow();
        ApplyHudInfo(hudInfo, refreshStyle: false);
        _viewModel!.Points = ToPointCollection(points);
        ShowWindow();
#if DEBUG
        hudWatch.Stop();
        System.Diagnostics.Trace.WriteLine(
            $"GesturePerf HudUpdateDurationMs ElapsedMs={hudWatch.ElapsedMilliseconds} PointCount={points.Count}");
#endif
    }

    private int BeginPresentation()
    {
        lock (_updateSyncRoot)
        {
            _generation++;
            _pendingUpdatePoints = null;
            _pendingUpdateHudInfo = null;
            _updateQueued = false;
            return _generation;
        }
    }

    private void ClearPendingUpdate(int generation)
    {
        lock (_updateSyncRoot)
        {
            if (generation != _generation)
            {
                return;
            }

            _pendingUpdatePoints = null;
            _pendingUpdateHudInfo = null;
            _updateQueued = false;
        }
    }

    private PointCollection ToPointCollection(IReadOnlyList<GesturePoint> points)
    {
        var collection = new PointCollection(points.Count);
        foreach (var point in points)
        {
            collection.Add(ToOverlayPoint(point));
        }

        collection.Freeze();
        return collection;
    }

    private System.Windows.Point ToOverlayPoint(GesturePoint point)
    {
        if (_window is null)
        {
            return new System.Windows.Point(point.X, point.Y);
        }

        // Hook coordinates are physical screen pixels, while WPF renders in device-independent units.
        // PointFromScreen keeps the trail attached to the cursor under DPI scaling and multi-monitor layouts.
        return _window.PointFromScreen(new System.Windows.Point(point.X, point.Y));
    }

    private static IReadOnlyList<GesturePoint> TrimVisiblePoints(IReadOnlyList<GesturePoint> points)
    {
        if (points.Count <= MaxVisiblePointCount)
        {
            return points;
        }

        return points.Skip(points.Count - MaxVisiblePointCount).ToArray();
    }

}
