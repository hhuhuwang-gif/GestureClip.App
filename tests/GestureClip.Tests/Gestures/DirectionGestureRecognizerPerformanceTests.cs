using System.Collections;
using GestureClip.Core.Gestures;
using GestureClip.Features.Gestures;
using Xunit;

namespace GestureClip.Tests.Gestures;

public sealed class DirectionGestureRecognizerPerformanceTests
{
    private const int PointCount = 96;
    private static readonly GestureOptions Options = new(20, 16, 2000, 2);

    [Fact]
    public void Straight_stroke_reads_each_point_once()
    {
        var points = new CountingPoints(Enumerable.Range(0, PointCount)
            .Select(index => Point(index * 20, 0, index)).ToArray());

        var result = new DirectionGestureRecognizer().Recognize(points, Options);

        Assert.Equal("R", result.Pattern);
        Assert.True(result.IsValid);
        Assert.Equal(PointCount, points.ReadCount);
    }

    [Fact]
    public void Turning_stroke_reads_each_point_at_most_twice()
    {
        var points = new CountingPoints(Enumerable.Range(0, PointCount)
            .Select(index => index < PointCount / 2
                ? Point(index * 20, 0, index)
                : Point((PointCount / 2 - 1) * 20, (index - PointCount / 2 + 1) * 20, index))
            .ToArray());

        var result = new DirectionGestureRecognizer().Recognize(points, Options);

        Assert.Equal("RD", result.Pattern);
        Assert.True(result.IsValid);
        Assert.InRange(points.ReadCount, PointCount, PointCount * 2);
    }

    [Theory]
    [InlineData(8, true)]
    [InlineData(9, false)]
    public void Alternating_pattern_retains_the_full_pattern_when_invalid(int segmentCount, bool expectedValid)
    {
        var points = Enumerable.Range(0, segmentCount + 1)
            .Select(index => Point(index % 2 * 40, index / 2 * 40, index))
            .ToArray();

        var result = new DirectionGestureRecognizer().Recognize(points, Options);

        Assert.Equal(string.Concat(Enumerable.Range(0, segmentCount)
            .Select(index => index % 2 == 0 ? "R" : "L")), result.Pattern);
        Assert.Equal(expectedValid, result.IsValid);
    }

    private static GesturePoint Point(int x, int y, int milliseconds) =>
        new(x, y, DateTimeOffset.UnixEpoch.AddMilliseconds(milliseconds));

    private sealed class CountingPoints(IReadOnlyList<GesturePoint> points) : IReadOnlyList<GesturePoint>
    {
        public int Count => points.Count;
        public int ReadCount { get; private set; }

        public GesturePoint this[int index]
        {
            get
            {
                ReadCount++;
                return points[index];
            }
        }

        public IEnumerator<GesturePoint> GetEnumerator() => points.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
