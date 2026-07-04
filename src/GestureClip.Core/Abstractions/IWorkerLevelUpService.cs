using GestureClip.Core.WorkerLevel;

namespace GestureClip.Core.Abstractions;

public interface IWorkerLevelUpService
{
    Task ShowLevelUpAsync(WorkerLevelSnapshot snapshot, CancellationToken cancellationToken);
}
