using GestureClip.Core.Gestures;
using GestureClip.Features.Gestures;
using Xunit;

namespace GestureClip.Tests.Gestures;

public sealed class DirectionGestureRecognizerTests
{
    private static readonly GestureOptions Options = new(
        TriggerThreshold: 20,
        SegmentThreshold: 16,
        MaxDurationMs: 2000,
        MinGesturePoints: 2);

    [Theory]
    [InlineData(0, 0, -40, 0, "L")]
    [InlineData(0, 0, 40, 0, "R")]
    [InlineData(0, 0, 0, -40, "U")]
    [InlineData(0, 0, 0, 40, "D")]
    public void Recognize_detects_single_direction(int x1, int y1, int x2, int y2, string expected)
    {
        var recognizer = new DirectionGestureRecognizer();

        var result = recognizer.Recognize([Point(x1, y1, 0), Point(x2, y2, 100)], Options);

        Assert.True(result.IsValid);
        Assert.Equal(expected, result.Pattern);
    }

    [Theory]
    [InlineData("LR", 0, 0, -40, 0, 0, 0)]
    [InlineData("RL", 0, 0, 40, 0, 0, 0)]
    [InlineData("UD", 0, 0, 0, -40, 0, 0)]
    [InlineData("DU", 0, 0, 0, 40, 0, 0)]
    public void Recognize_detects_allowed_two_direction_patterns(
        string expected,
        int x1,
        int y1,
        int x2,
        int y2,
        int x3,
        int y3)
    {
        var recognizer = new DirectionGestureRecognizer();

        var result = recognizer.Recognize([Point(x1, y1, 0), Point(x2, y2, 100), Point(x3, y3, 200)], Options);

        Assert.True(result.IsValid);
        Assert.Equal(expected, result.Pattern);
    }

    [Fact]
    public void Recognize_merges_consecutive_same_direction_segments()
    {
        var recognizer = new DirectionGestureRecognizer();

        var result = recognizer.Recognize([Point(0, 0, 0), Point(30, 0, 100), Point(65, 0, 200)], Options);

        Assert.True(result.IsValid);
        Assert.Equal("R", result.Pattern);
    }

    [Fact]
    public void Recognize_ignores_short_segments()
    {
        var recognizer = new DirectionGestureRecognizer();

        var result = recognizer.Recognize([Point(0, 0, 0), Point(4, 0, 100), Point(50, 0, 200)], Options);

        Assert.True(result.IsValid);
        Assert.Equal("R", result.Pattern);
    }

    [Fact]
    public void Recognize_rejects_unlisted_patterns()
    {
        var recognizer = new DirectionGestureRecognizer();

        var result = recognizer.Recognize([Point(0, 0, 0), Point(40, 0, 100), Point(40, 40, 200)], Options);

        Assert.False(result.IsValid);
        Assert.Equal("RD", result.Pattern);
    }

    [Fact]
    public void Recognize_rejects_gestures_over_max_duration()
    {
        var recognizer = new DirectionGestureRecognizer();

        var result = recognizer.Recognize([Point(0, 0, 0), Point(40, 0, 2500)], Options);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Recognize_rejects_when_point_count_is_too_small()
    {
        var recognizer = new DirectionGestureRecognizer();

        var result = recognizer.Recognize([Point(0, 0, 0)], Options);

        Assert.False(result.IsValid);
    }

    private static GesturePoint Point(int x, int y, int milliseconds)
    {
        return new GesturePoint(x, y, DateTimeOffset.UnixEpoch.AddMilliseconds(milliseconds));
    }
}
