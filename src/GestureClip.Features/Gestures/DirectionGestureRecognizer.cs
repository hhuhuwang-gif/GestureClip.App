using GestureClip.Core.Abstractions;
using GestureClip.Core.Gestures;
using System.Runtime.InteropServices;

namespace GestureClip.Features.Gestures;

public sealed class DirectionGestureRecognizer : IMouseGestureRecognizer
{
    private const int MaxPatternSegments = 8;
    private const double OrthogonalNoiseFactor = 1.25;
    private const int DominantAxisFactor = 2;

    public GestureResult Recognize(IReadOnlyList<GesturePoint> points, GestureOptions options)
    {
        if (points.Count < options.MinGesturePoints)
        {
            return GestureResult.Invalid();
        }

        var first = points[0];
        var last = points[^1];
        var duration = last.Time - first.Time;
        if (duration.TotalMilliseconds > options.MaxDurationMs)
        {
            return GestureResult.Invalid();
        }

        var geometry = AnalyzeStroke(points, first, last);
        if (geometry.MaxDistanceSquared < Square(options.TriggerThreshold))
        {
            return GestureResult.Invalid();
        }

        if (TryRecognizeStableStraightStroke(geometry, options, out var straightDirection))
        {
            return new GestureResult(straightDirection, true);
        }

        return RecognizeDirections(points, options, first);
    }

    private static GestureResult RecognizeDirections(
        IReadOnlyList<GesturePoint> points,
        GestureOptions options,
        GesturePoint first)
    {
        var directions = new List<char>();
        var previous = first;
        var segmentThresholdSquared = Square(options.SegmentThreshold);
        for (var index = 1; index < points.Count; index++)
        {
            var current = points[index];
            var dx = current.X - previous.X;
            var dy = current.Y - previous.Y;
            previous = current;

            if (((double)dx * dx) + ((double)dy * dy) < segmentThresholdSquared)
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

        var pattern = new string(CollectionsMarshal.AsSpan(directions));
        return new GestureResult(pattern, pattern.Length <= MaxPatternSegments);
    }

    private static bool TryRecognizeStableStraightStroke(
        StrokeGeometry geometry,
        GestureOptions options,
        out string? direction)
    {
        var dx = geometry.End.X - geometry.Start.X;
        var dy = geometry.End.Y - geometry.Start.Y;
        var allowedOrthogonalNoise = options.SegmentThreshold * OrthogonalNoiseFactor;

        if (Math.Abs(dx) >= options.TriggerThreshold &&
            geometry.VerticalSpan <= allowedOrthogonalNoise &&
            geometry.HorizontalSpan >= geometry.VerticalSpan * DominantAxisFactor)
        {
            direction = dx < 0 ? "L" : "R";
            return true;
        }

        if (Math.Abs(dy) >= options.TriggerThreshold &&
            geometry.HorizontalSpan <= allowedOrthogonalNoise &&
            geometry.VerticalSpan >= geometry.HorizontalSpan * DominantAxisFactor)
        {
            direction = dy < 0 ? "U" : "D";
            return true;
        }

        direction = null;
        return false;
    }

    private static StrokeGeometry AnalyzeStroke(
        IReadOnlyList<GesturePoint> points,
        GesturePoint first,
        GesturePoint last)
    {
        var minX = first.X;
        var maxX = first.X;
        var minY = first.Y;
        var maxY = first.Y;

        var maxDistanceSquared = 0d;
        var lastIndex = points.Count - 1;
        for (var index = 1; index <= lastIndex; index++)
        {
            var point = index == lastIndex ? last : points[index];
            minX = Math.Min(minX, point.X);
            maxX = Math.Max(maxX, point.X);
            minY = Math.Min(minY, point.Y);
            maxY = Math.Max(maxY, point.Y);
            var dx = (double)point.X - first.X;
            var dy = (double)point.Y - first.Y;
            maxDistanceSquared = Math.Max(maxDistanceSquared, (dx * dx) + (dy * dy));
        }

        return new StrokeGeometry
        {
            Start = first,
            End = last,
            MaxDistanceSquared = maxDistanceSquared,
            HorizontalSpan = maxX - minX,
            VerticalSpan = maxY - minY
        };
    }

    private static double Square(int value) => (double)value * value;

    private readonly record struct StrokeGeometry
    {
        public required GesturePoint Start { get; init; }
        public required GesturePoint End { get; init; }
        public double MaxDistanceSquared { get; init; }
        public int HorizontalSpan { get; init; }
        public int VerticalSpan { get; init; }
    }
}
