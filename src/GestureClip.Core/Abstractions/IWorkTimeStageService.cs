using GestureClip.Core.Workstation;

namespace GestureClip.Core.Abstractions;

public interface IWorkTimeStageService
{
    WorkTimeStageSnapshot GetSnapshot(DateTimeOffset now);
}
