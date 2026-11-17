using GestureClip.Core.Gestures;
using GestureClip.Features.Gestures;
using Xunit;

namespace GestureClip.Tests.Gestures;

public sealed class ChevronGestureRecognizerTests
{
    private static readonly GestureOptions Options = new(20, 12, 2000, 3);
    private static readonly (int X, int Y)[] LeftChevron = [(120, 0), (40, 80), (120, 160)];

    [Theory]
    [InlineData(false, false, "<")]
    [InlineData(false, true, "<")]
    [InlineData(true, false, ">")]
    [InlineData(true, true, ">")]
    public void Recognizes_both_chevrons_in_either_drawing_direction(bool mirror, bool reverse, string expected)
    {
        var coordinates = LeftChevron.Select(point => (mirror ? -point.X : point.X, point.Y)).ToArray();
        if (reverse) Array.Reverse(coordinates);
        var result = Recognize(coordinates);
        Assert.True(result.IsValid);
        Assert.Equal(expected, result.Pattern);
    }

    [Theory]
    [InlineData(false, "<")]
    [InlineData(true, ">")]
    public void Dense_sampling_and_small_hand_jitter_preserve_the_chevron(bool mirror, string expected)
    {
        var coordinates = Enumerable.Range(0, 41).Select(index =>
        {
            var y = index * 4;
            var x = Math.Abs(y - 80) + 40 + (index % 3 - 1) * 2;
            return (mirror ? -x : x, y);
        }).ToArray();
        var result = Recognize(coordinates);
        Assert.True(result.IsValid);
        Assert.Equal(expected, result.Pattern);
    }

    [Theory]
    [MemberData(nameof(OtherShapes))]
    public void Existing_strokes_and_curved_shapes_do_not_become_macro_chevrons((int X, int Y)[] coordinates)
    {
        var result = Recognize(coordinates);
        Assert.NotEqual("<", result.Pattern);
        Assert.NotEqual(">", result.Pattern);
    }

    public static IEnumerable<object[]> OtherShapes()
    {
        yield return [new (int, int)[] { (0, 0), (-80, 0), (0, 0) }]; // LR
        yield return [new (int, int)[] { (0, 0), (80, 0), (0, 0) }]; // RL
        yield return [new (int, int)[] { (0, 0), (80, 80), (160, 0) }]; // V
        yield return [new (int, int)[] { (0, 0), (0, 80), (80, 80), (80, 0) }]; // U
        yield return [new (int, int)[] { (0, 0), (80, 0), (80, 80) }]; // L
        yield return [new (int, int)[] { (0, 0), (40, 40), (80, 80) }]; // diagonal
        yield return [new (int, int)[] { (100, 0), (0, 20), (0, 80), (100, 100) }]; // square C
        yield return [new (int, int)[] { (100, 0), (20, 30), (0, 80), (20, 130), (100, 160) }]; // rounded C
        yield return [new (int, int)[] { (120, 0), (80, 40), (100, 20), (40, 80), (120, 160) }]; // retracing
    }

    [Fact]
    public void Chevron_respects_the_existing_duration_and_point_count_requirements()
    {
        var points = Points(LeftChevron);
        var recognizer = new DirectionGestureRecognizer();
        Assert.False(recognizer.Recognize(points, Options with { MinGesturePoints = 4 }).IsValid);
        Assert.False(recognizer.Recognize(points, Options with { MaxDurationMs = 10 }).IsValid);
        Assert.False(recognizer.Recognize(Points([(12, 0), (4, 8), (12, 16)]), Options).IsValid);
    }

    private static GestureResult Recognize((int X, int Y)[] coordinates) =>
        new DirectionGestureRecognizer().Recognize(Points(coordinates), Options);

    private static GesturePoint[] Points((int X, int Y)[] coordinates) => coordinates
        .Select((point, index) => new GesturePoint(point.X, point.Y, DateTimeOffset.UnixEpoch.AddMilliseconds(index * 10))).ToArray();
}
