using GestureClip.Core.Gestures;
using GestureClip.Features.Gestures;
using Xunit;

namespace GestureClip.Tests.Gestures;

public sealed class NaturalChevronGestureRecognizerTests
{
    private static readonly GestureOptions Options = new(20, 12, 2000, 3);
    private const int DensificationFactor = 4;
    private const int SampleIntervalMilliseconds = 10;

    [Theory]
    [MemberData(nameof(NaturalStrokes))]
    public void Natural_chevrons_allow_gentle_curves_and_unequal_legs((int X, int Y)[] coordinates)
    {
        foreach (var mirror in new[] { false, true })
        {
            foreach (var reverse in new[] { false, true })
            {
                var points = Transform(coordinates, mirror, reverse);
                var result = new DirectionGestureRecognizer().Recognize(points, Options);
                Assert.True(result.IsValid);
                Assert.Equal(mirror ? ">" : "<", result.Pattern);
            }
        }
    }

    [Fact]
    public void Natural_curve_recognition_does_not_depend_on_sample_density()
    {
        var coordinates = ((int X, int Y)[])NaturalStrokes().First()[0];
        var dense = coordinates.Zip(coordinates.Skip(1), (start, end) => Enumerable.Range(0, DensificationFactor)
            .Select(step => (start.X + (end.X - start.X) * step / DensificationFactor,
                start.Y + (end.Y - start.Y) * step / DensificationFactor))).SelectMany(points => points)
            .Append(coordinates[^1]).ToArray();
        var result = new DirectionGestureRecognizer().Recognize(Transform(dense, false, false), Options);
        Assert.Equal("<", result.Pattern);
    }

    [Theory]
    [MemberData(nameof(ChevronGestureRecognizerTests.OtherShapes), MemberType = typeof(ChevronGestureRecognizerTests))]
    public void Unequal_leg_support_does_not_convert_other_strokes_to_chevrons((int X, int Y)[] coordinates)
    {
        foreach (var mirror in new[] { false, true })
        {
            foreach (var reverse in new[] { false, true })
            {
                var result = new DirectionGestureRecognizer().Recognize(Transform(coordinates, mirror, reverse), Options);
                Assert.NotEqual("<", result.Pattern);
                Assert.NotEqual(">", result.Pattern);
            }
        }
    }

    public static IEnumerable<object[]> NaturalStrokes()
    {
        yield return [new (int, int)[]
        {
            (120, 0), (100, 12), (80, 28), (60, 50), (44, 74),
            (40, 84), (48, 94), (68, 112), (96, 136), (120, 160)
        }];
        yield return [new (int, int)[] { (120, 0), (80, 40), (40, 80), (100, 110), (160, 140), (220, 170) }];
        yield return [new (int, int)[]
        {
            (120, 0), (98, 28), (80, 32), (58, 64), (40, 80),
            (62, 92), (80, 128), (102, 132), (120, 160)
        }];
    }

    private static GesturePoint[] Transform((int X, int Y)[] coordinates, bool mirror, bool reverse)
    {
        var ordered = reverse ? coordinates.Reverse() : coordinates;
        return ordered.Select((point, index) => new GesturePoint(mirror ? -point.X : point.X,
            point.Y, DateTimeOffset.UnixEpoch.AddMilliseconds(index * SampleIntervalMilliseconds))).ToArray();
    }
}
