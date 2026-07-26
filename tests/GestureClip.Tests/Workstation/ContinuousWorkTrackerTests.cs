using GestureClip.Core.Abstractions;
using GestureClip.Features.Workstation;
using Xunit;

namespace GestureClip.Tests.Workstation;

public sealed class ContinuousWorkTrackerTests
{
    private sealed class FakeIdleProvider : IUserIdleProvider
    {
        public TimeSpan Idle { get; set; } = TimeSpan.Zero;
        public TimeSpan GetIdleDuration() => Idle;
    }

    private static readonly DateTimeOffset T0 = new(2026, 7, 27, 10, 0, 0, TimeSpan.FromHours(8));

    [Fact]
    public void Active_user_accumulates_continuous_work()
    {
        var idle = new FakeIdleProvider();
        var tracker = new ContinuousWorkTracker(idle);

        Assert.Equal(TimeSpan.Zero, tracker.GetContinuousWorkDuration(T0));

        idle.Idle = TimeSpan.FromSeconds(30);
        var afterTen = tracker.GetContinuousWorkDuration(T0.AddMinutes(10));

        Assert.Equal(10, afterTen.TotalMinutes, precision: 0);
    }

    [Fact]
    public void Short_typing_pauses_do_not_reset_the_segment()
    {
        var idle = new FakeIdleProvider();
        var tracker = new ContinuousWorkTracker(idle);
        tracker.GetContinuousWorkDuration(T0);

        // Sampled periodically (as the dashboard does); a 2-minute pause mid-way.
        idle.Idle = TimeSpan.FromMinutes(2);
        tracker.GetContinuousWorkDuration(T0.AddMinutes(10));
        idle.Idle = TimeSpan.FromSeconds(5);
        tracker.GetContinuousWorkDuration(T0.AddMinutes(20));
        var result = tracker.GetContinuousWorkDuration(T0.AddMinutes(30));

        Assert.True(result.TotalMinutes >= 29);
    }

    [Fact]
    public void Idle_beyond_threshold_counts_as_rest_and_returns_zero()
    {
        var idle = new FakeIdleProvider();
        var tracker = new ContinuousWorkTracker(idle);
        tracker.GetContinuousWorkDuration(T0);

        idle.Idle = ContinuousWorkTracker.RestThreshold;
        Assert.Equal(TimeSpan.Zero, tracker.GetContinuousWorkDuration(T0.AddMinutes(40)));
    }

    [Fact]
    public void Resuming_after_rest_starts_a_fresh_segment()
    {
        var idle = new FakeIdleProvider();
        var tracker = new ContinuousWorkTracker(idle);
        tracker.GetContinuousWorkDuration(T0);

        idle.Idle = TimeSpan.FromMinutes(6);
        tracker.GetContinuousWorkDuration(T0.AddMinutes(60));

        // User came back 1 minute ago.
        idle.Idle = TimeSpan.FromMinutes(1);
        var result = tracker.GetContinuousWorkDuration(T0.AddMinutes(65));

        Assert.Equal(1, result.TotalMinutes, precision: 0);
    }

    [Fact]
    public void Large_sampling_gap_resets_the_segment()
    {
        var idle = new FakeIdleProvider();
        var tracker = new ContinuousWorkTracker(idle);
        tracker.GetContinuousWorkDuration(T0);

        // Machine slept for 2 hours; on wake idle reads small again.
        idle.Idle = TimeSpan.FromSeconds(10);
        var result = tracker.GetContinuousWorkDuration(T0.AddHours(2));

        Assert.True(result < TimeSpan.FromMinutes(1));
    }
}
