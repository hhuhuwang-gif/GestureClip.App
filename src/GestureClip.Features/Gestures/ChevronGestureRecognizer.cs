using GestureClip.Core.Gestures;

namespace GestureClip.Features.Gestures;

internal readonly record struct ChevronStrokeGeometry
{
    public required GesturePoint Start { get; init; }
    public required GesturePoint End { get; init; }
    public required GesturePoint LeftTip { get; init; }
    public required GesturePoint RightTip { get; init; }
    public int LeftTipIndex { get; init; }
    public int RightTipIndex { get; init; }
}

internal static class ChevronGestureRecognizer
{
    private const double MinimumLegFraction = 0.5;
    private const double MaximumLegSlopeRatio = 4;
    private const double RelativeLineTolerance = 0.08;
    private const double SegmentNoiseFraction = 0.5;
    private const double LinearSegmentIntegralDivisor = 3;

    public static string? Recognize(IReadOnlyList<GesturePoint> points, GestureOptions options, ChevronStrokeGeometry geometry)
    {
        if (Math.Abs((double)geometry.End.Y - geometry.Start.Y) < options.TriggerThreshold) return null;
        if (Matches(points, options, new ChevronCandidate(geometry.Start, geometry.End)
            { Tip = geometry.LeftTip, TipIndex = geometry.LeftTipIndex })) return "<";
        return Matches(points, options, new ChevronCandidate(geometry.Start, geometry.End)
            { Tip = geometry.RightTip, TipIndex = geometry.RightTipIndex }) ? ">" : null;
    }

    private static bool Matches(IReadOnlyList<GesturePoint> points, GestureOptions options, ChevronCandidate candidate)
    {
        if (candidate.TipIndex == 0 || candidate.TipIndex == points.Count - 1) return false;
        var firstVertical = (double)candidate.Tip.Y - candidate.Start.Y;
        var lastVertical = (double)candidate.End.Y - candidate.Tip.Y;
        if (Math.Sign(firstVertical) != Math.Sign(lastVertical)) return false;
        var minimumLegTravel = Math.Max(options.SegmentThreshold, options.TriggerThreshold * MinimumLegFraction);
        if (!IsDiagonal(candidate.Start, candidate.Tip, minimumLegTravel)) return false;
        if (!IsDiagonal(candidate.Tip, candidate.End, minimumLegTravel)) return false;
        var first = new ChevronLeg(candidate.Start, candidate.Tip) { StartIndex = 0, EndIndex = candidate.TipIndex };
        var last = new ChevronLeg(candidate.Tip, candidate.End) { StartIndex = candidate.TipIndex, EndIndex = points.Count - 1 };
        return FitsLeg(points, options, first) && FitsLeg(points, options, last);
    }

    private static bool IsDiagonal(GesturePoint start, GesturePoint end, double minimumTravel)
    {
        var horizontal = Math.Abs((double)end.X - start.X);
        var vertical = Math.Abs((double)end.Y - start.Y);
        return horizontal >= minimumTravel && vertical >= minimumTravel
            && horizontal <= vertical * MaximumLegSlopeRatio && vertical <= horizontal * MaximumLegSlopeRatio;
    }

    private static bool FitsLeg(IReadOnlyList<GesturePoint> points, GestureOptions options, ChevronLeg leg)
    {
        var dx = (double)leg.End.X - leg.Start.X;
        var dy = (double)leg.End.Y - leg.Start.Y;
        var length = Math.Sqrt(dx * dx + dy * dy);
        var tolerance = Math.Max(options.SegmentThreshold * SegmentNoiseFraction, length * RelativeLineTolerance);
        var furthest = 0d;
        var previousPoint = leg.Start;
        var previousDeviation = 0d;
        var pathLength = 0d;
        var deviationIntegral = 0d;
        for (var index = leg.StartIndex + 1; index <= leg.EndIndex; index++)
        {
            var point = points[index];
            var px = (double)point.X - leg.Start.X;
            var py = (double)point.Y - leg.Start.Y;
            var along = (px * dx + py * dy) / length;
            var deviation = (px * dy - py * dx) / length;
            if (along < furthest - tolerance || along > length + tolerance) return false;
            var segmentLength = Distance(previousPoint, point);
            deviationIntegral += segmentLength * (previousDeviation * previousDeviation
                + previousDeviation * deviation + deviation * deviation) / LinearSegmentIntegralDivisor;
            pathLength += segmentLength;
            previousPoint = point;
            previousDeviation = deviation;
            furthest = Math.Max(furthest, along);
        }
        // Integrate along the actual path so jitter is not rejected by one point or weighted by sampling density.
        return deviationIntegral <= tolerance * tolerance * pathLength;
    }

    private static double Distance(GesturePoint start, GesturePoint end)
    {
        var dx = (double)end.X - start.X;
        var dy = (double)end.Y - start.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    private readonly record struct ChevronCandidate(GesturePoint Start, GesturePoint End)
    {
        public required GesturePoint Tip { get; init; }
        public int TipIndex { get; init; }
    }

    private readonly record struct ChevronLeg(GesturePoint Start, GesturePoint End)
    {
        public int StartIndex { get; init; }
        public int EndIndex { get; init; }
    }
}
