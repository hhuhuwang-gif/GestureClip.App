using GestureClip.Core.Workstation;

namespace GestureClip.Core.Abstractions;

public interface IWorkstationStatsRepository
{
    Task<WorkstationDailyStats> GetOrCreateAsync(DateOnly date, CancellationToken cancellationToken);

    Task SaveAsync(WorkstationDailyStats stats, CancellationToken cancellationToken);

    Task ResetAsync(DateOnly date, CancellationToken cancellationToken);
}
