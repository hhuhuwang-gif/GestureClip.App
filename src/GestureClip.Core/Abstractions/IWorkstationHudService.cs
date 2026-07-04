using GestureClip.Core.Gestures;
using GestureClip.Core.Workstation;

namespace GestureClip.Core.Abstractions;

public interface IWorkstationHudService
{
    Task<WorkstationHudSnapshot> BuildSnapshotAsync(
        GestureHudInfo hudInfo,
        int gainedXp,
        DateTimeOffset now,
        CancellationToken cancellationToken);
}
