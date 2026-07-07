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

        if (MaxDistanceFromStart(points) < options.TriggerThreshold)
        {
            return GestureResult.Invalid();
        }

        if (TryRecognizeStableStraightStroke(points, options, out var straightDirection))
        {
            return new GestureResult(straightDirection.ToString(), true);
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

    private static bool TryRecognizeStableStraightStroke(
        IReadOnlyList<GesturePoint> points,
        GestureOptions options,
        out char direction)
    {
        var first = points[0];
        var last = points[^1];
        var dx = last.X - first.X;
        var dy = last.Y - first.Y;
        var minX = first.X;
        var maxX = first.X;
        var minY = first.Y;
        var maxY = first.Y;

        for (var i = 1; i < points.Count; i++)
        {
            var point = points[i];
            minX = Math.Min(minX, point.X);
            maxX = Math.Max(maxX, point.X);
            minY = Math.Min(minY, point.Y);
            maxY = Math.Max(maxY, point.Y);
        }

        var horizontalSpan = maxX - minX;
        var verticalSpan = maxY - minY;
        var allowedOrthogonalNoise = options.SegmentThreshold * 1.25;

        if (Math.Abs(dx) >= options.TriggerThreshold &&
            verticalSpan <= allowedOrthogonalNoise &&
            horizontalSpan >= verticalSpan * 2)
        {
            direction = dx < 0 ? 'L' : 'R';
            return true;
        }

        if (Math.Abs(dy) >= options.TriggerThreshold &&
            horizontalSpan <= allowedOrthogonalNoise &&
            verticalSpan >= horizontalSpan * 2)
        {
            direction = dy < 0 ? 'U' : 'D';
            return true;
        }

        direction = default;
        return false;
    }

    private static double Distance(GesturePoint first, GesturePoint second)
    {
        var dx = second.X - first.X;
        var dy = second.Y - first.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    private static double MaxDistanceFromStart(IReadOnlyList<GesturePoint> points)
    {
        var maxDistance = 0d;
        var start = points[0];
        for (var i = 1; i < points.Count; i++)
        {
            maxDistance = Math.Max(maxDistance, Distance(start, points[i]));
        }

        return maxDistance;
    }
}
