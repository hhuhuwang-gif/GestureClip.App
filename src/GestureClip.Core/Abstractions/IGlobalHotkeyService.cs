using GestureClip.Core.Hotkeys;

namespace GestureClip.Core.Abstractions;

public interface IGlobalHotkeyService
{
    HotkeyStatus Status { get; }

    void Start();

    void Stop();
}
