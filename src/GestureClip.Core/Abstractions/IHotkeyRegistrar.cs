namespace GestureClip.Core.Abstractions;

public interface IHotkeyRegistrar
{
    event EventHandler? HotkeyPressed;

    bool RegisterOpenClipboardHotkey();

    void UnregisterOpenClipboardHotkey();

    int GetLastError();
}
