using GestureClip.Core.Abstractions;

namespace GestureClip.Features.Workstation;

/// <summary>
/// Activity-driven continuous work: the segment starts when input activity resumes
/// and resets after an idle gap of <see cref="RestThreshold"/>. Only idle duration
/// is sampled — no window or app identification.
/// </summary>
public sealed class ContinuousWorkTracker : IContinuousWorkTracker
{
    /// <summary>Idle gap that counts as a real rest.</summary>
    public static readonly TimeSpan RestThreshold = TimeSpan.FromMinutes(5);

    /// <summary>A sampling gap larger than this (sleep/hibernate) also resets the segment.</summary>
    private static readonly TimeSpan SampleGapReset = TimeSpan.FromMinutes(10);

    private readonly IUserIdleProvider _idleProvider;
    private readonly object _syncRoot = new();
    private DateTimeOffset? _segmentStart;
    private DateTimeOffset _lastSampleAt = DateTimeOffset.MinValue;

    public ContinuousWorkTracker(IUserIdleProvider idleProvider)
    {
        _idleProvider = idleProvider;
    }

    public TimeSpan GetContinuousWorkDuration(DateTimeOffset now)
    {
        lock (_syncRoot)
        {
            var idle = _idleProvider.GetIdleDuration();
            if (idle >= RestThreshold)
            {
                // Resting right now; next activity starts a fresh segment.
                _segmentStart = null;
                _lastSampleAt = now;
                return TimeSpan.Zero;
            }

            if (_segmentStart is null || now - _lastSampleAt > SampleGapReset)
            {
                // Segment began when activity resumed (idle time ago).
                _segmentStart = now - idle;
            }

            _lastSampleAt = now;
            var duration = now - _segmentStart.Value;
            return duration > TimeSpan.Zero ? duration : TimeSpan.Zero;
        }
    }
}
