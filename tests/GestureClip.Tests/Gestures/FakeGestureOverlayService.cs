using System.Collections.Concurrent;
using GestureClip.Core.Abstractions;
using GestureClip.Core.Gestures;

namespace GestureClip.Tests.Gestures;

internal sealed class FakeGestureOverlayService : IGestureOverlayService
{
    private readonly ConcurrentQueue<string> _events = [];
    private readonly ConcurrentQueue<GestureHudInfo> _hudInfos = [];
    private readonly ConcurrentQueue<int> _pointCounts = [];
    private readonly ConcurrentQueue<IReadOnlyList<GesturePoint>> _pointSnapshots = [];

    public IReadOnlyList<string> Events => _events.ToArray();

    public IReadOnlyList<GestureHudInfo> HudInfos => _hudInfos.ToArray();

    public IReadOnlyList<int> PointCounts => _pointCounts.ToArray();

    public IReadOnlyList<IReadOnlyList<GesturePoint>> PointSnapshots => _pointSnapshots.ToArray();

    public Task ShowGestureStartAsync(GesturePoint point, GestureHudInfo hudInfo, CancellationToken cancellationToken)
    {
        _hudInfos.Enqueue(hudInfo);
        _pointCounts.Enqueue(1);
        _pointSnapshots.Enqueue([point]);
        _events.Enqueue($"start:{point.X},{point.Y}");
        return Task.CompletedTask;
    }

    public Task UpdateGestureAsync(IReadOnlyList<GesturePoint> points, GestureHudInfo hudInfo, CancellationToken cancellationToken)
    {
        _hudInfos.Enqueue(hudInfo);
        _pointCounts.Enqueue(points.Count);
        _pointSnapshots.Enqueue([.. points]);
        _events.Enqueue($"update:{points.Count}:{hudInfo.Pattern}");
        return Task.CompletedTask;
    }

    public Task CompleteGestureAsync(GestureHudInfo hudInfo, CancellationToken cancellationToken)
    {
        _hudInfos.Enqueue(hudInfo);
        _events.Enqueue($"complete:{hudInfo.Pattern}");
        return Task.CompletedTask;
    }

    public Task ShowPatternAsync(string pattern, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task HideAsync(CancellationToken cancellationToken)
    {
        _events.Enqueue("hide");
        return Task.CompletedTask;
    }
}
