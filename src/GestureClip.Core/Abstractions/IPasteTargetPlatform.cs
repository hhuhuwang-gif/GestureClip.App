using GestureClip.Core.SystemInfo;

namespace GestureClip.Core.Abstractions;

public interface IPasteTargetPlatform
{
    PasteTarget CaptureTarget();
    nint ForegroundWindow { get; }
    bool ArePasteKeysReleased { get; }
    bool IsWindow(nint windowHandle);
    bool Activate(nint windowHandle);
}
