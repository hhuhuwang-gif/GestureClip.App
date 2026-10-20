using GestureClip.Core.SystemInfo;

namespace GestureClip.Core.Abstractions;

public interface IPasteTargetService
{
    PasteTarget CaptureTarget();
    Task WaitUntilReadyAsync(PasteTarget target, CancellationToken cancellationToken);
}
