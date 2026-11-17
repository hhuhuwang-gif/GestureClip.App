using GestureClip.Core.Gestures;

namespace GestureClip.Features.Gestures;

public sealed partial class MouseGestureService
{
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
