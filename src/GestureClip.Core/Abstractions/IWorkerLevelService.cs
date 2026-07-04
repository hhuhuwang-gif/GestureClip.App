using GestureClip.Core.Gestures;
using GestureClip.Core.WorkerLevel;

namespace GestureClip.Core.Abstractions;

public interface IWorkerLevelService
{
    Task<WorkerLevelSnapshot> GetSnapshotAsync(CancellationToken cancellationToken);

    Task<WorkerLevelSnapshot> RecordActionAsync(
        BuiltInGestureAction action,
        bool isGestureSuccess,
        DateTimeOffset now,
        CancellationToken cancellationToken);
}
