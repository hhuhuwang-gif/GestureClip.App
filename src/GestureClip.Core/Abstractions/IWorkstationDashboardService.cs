using GestureClip.Core.Workstation;

namespace GestureClip.Core.Abstractions;

public interface IWorkstationDashboardService
{
    Task<WorkstationDashboardSnapshot> GetSnapshotAsync(DateTimeOffset now, CancellationToken cancellationToken);

    Task StartFishingAsync(DateTimeOffset now, CancellationToken cancellationToken);

    Task EndFishingAsync(DateTimeOffset now, CancellationToken cancellationToken);

    Task ResetTodayAsync(DateOnly date, CancellationToken cancellationToken);

    Task RecordCopyAsync(DateTimeOffset now, CancellationToken cancellationToken);

    Task RecordPasteAsync(DateTimeOffset now, CancellationToken cancellationToken);

    Task RecordGestureAsync(DateTimeOffset now, CancellationToken cancellationToken);
}
