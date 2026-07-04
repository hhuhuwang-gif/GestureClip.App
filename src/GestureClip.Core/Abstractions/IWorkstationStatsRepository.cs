using GestureClip.Core.Workstation;

namespace GestureClip.Core.Abstractions;

public interface IWorkstationStatsRepository
{
    Task<WorkstationDailyStats> GetOrCreateAsync(DateOnly date, CancellationToken cancellationToken);

    Task SaveAsync(WorkstationDailyStats stats, CancellationToken cancellationToken);

    Task IncrementCountersAsync(
        DateOnly date,
        int copyDelta,
        int pasteDelta,
        int gestureDelta,
        int savedClicksDelta,
        CancellationToken cancellationToken);

    Task ResetAsync(DateOnly date, CancellationToken cancellationToken);
}
