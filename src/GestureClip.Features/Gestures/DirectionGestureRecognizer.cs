using GestureClip.Core.Abstractions;
using GestureClip.Core.Gestures;

namespace GestureClip.Features.Gestures;

public sealed class DirectionGestureRecognizer : IMouseGestureRecognizer
{
    private const int MaxPatternSegments = 8;

    public GestureResult Recognize(IReadOnlyList<GesturePoint> points, GestureOptions options)
    {
        if (points.Count < options.MinGesturePoints)
        {
            return GestureResult.Invalid();
        }

        var duration = points[^1].Time - points[0].Time;
        if (duration.TotalMilliseconds > options.MaxDurationMs)
        {
            return GestureResult.Invalid();
        }

        if (TotalDistance(points) < options.TriggerThreshold)
        {
            return GestureResult.Invalid();
        }

        var directions = new List<char>();
        for (var i = 1; i < points.Count; i++)
        {
            var previous = points[i - 1];
            var current = points[i];
            var dx = current.X - previous.X;
            var dy = current.Y - previous.Y;

            if (Math.Sqrt(dx * dx + dy * dy) < options.SegmentThreshold)
            {
                continue;
            }

            var direction = Math.Abs(dx) >= Math.Abs(dy)
                ? dx < 0 ? 'L' : 'R'
                : dy < 0 ? 'U' : 'D';

            if (directions.Count == 0 || directions[^1] != direction)
            {
                directions.Add(direction);
            }
        }

        if (directions.Count == 0)
        {
            return GestureResult.Invalid();
        }

        var pattern = new string(directions.ToArray());
        return new GestureResult(pattern, pattern.Length <= MaxPatternSegments);
    }

    private static double Distance(GesturePoint first, GesturePoint second)
    {
        var dx = second.X - first.X;
        var dy = second.Y - first.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    private static double TotalDistance(IReadOnlyList<GesturePoint> points)
    {
        var total = 0d;
        for (var i = 1; i < points.Count; i++)
        {
            total += Distance(points[i - 1], points[i]);
        }

        return total;
    }
}
