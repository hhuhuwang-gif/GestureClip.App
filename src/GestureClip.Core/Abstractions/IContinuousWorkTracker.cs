namespace GestureClip.Core.Abstractions;

/// <summary>
/// Tracks the current uninterrupted work segment based on real input activity:
/// an idle gap of a few minutes counts as a rest and resets the segment.
/// </summary>
public interface IContinuousWorkTracker
{
    /// <summary>Duration of the current continuous work segment; zero while resting.</summary>
    TimeSpan GetContinuousWorkDuration(DateTimeOffset now);
}
