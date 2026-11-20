using GestureClip.Core.Gestures;

namespace GestureClip.Features.Gestures;

public sealed partial class MouseGestureService
{
    private void PreserveCompactChevronTip(GesturePoint next, GestureOptions options)
    {
        if (_lastRawMove is not { } tip) return;
        var previous = _points[^1];
        var firstX = (double)tip.X - previous.X;
        var secondX = (double)next.X - tip.X;
        var firstY = (double)tip.Y - previous.Y;
        var secondY = (double)next.Y - tip.Y;
        var minimum = ChevronGestureRecognizer.GetMinimumLegTravel(options);

        // Keep a real reversal that would otherwise fall between normal distance samples.
        if (firstX * secondX >= 0 || firstY * secondY <= 0) return;
        if (Math.Min(Math.Abs(firstX), Math.Abs(secondX)) < minimum ||
            Math.Min(Math.Abs(firstY), Math.Abs(secondY)) < minimum) return;
        _points.Add(tip);
        TrimTrackedPoints();
    }

    private int FindLeastSignificantPoint()
    {
        var selected = 1;
        var smallestDeviation = double.PositiveInfinity;
        for (var index = 1; index < _points.Count - 1; index++)
        {
            var deviation = DistanceToSegmentSquared(_points[index], _points[index - 1], _points[index + 1]);
            if (deviation >= smallestDeviation) continue;
            smallestDeviation = deviation;
            selected = index;
        }
        return selected;
    }

    private static double DistanceToSegmentSquared(GesturePoint point, GesturePoint start, GesturePoint end)
    {
        var dx = (double)end.X - start.X;
        var dy = (double)end.Y - start.Y;
        var lengthSquared = dx * dx + dy * dy;
        if (lengthSquared == 0) return DistanceSquared(point, start);
        // Clamp to the segment: a collinear reversal is a turn, not redundant line detail.
        var projection = Math.Clamp(((point.X - start.X) * dx + (point.Y - start.Y) * dy) / lengthSquared, 0, 1);
        var offsetX = point.X - start.X - projection * dx;
        var offsetY = point.Y - start.Y - projection * dy;
        return offsetX * offsetX + offsetY * offsetY;
    }
}
