namespace GestureClip.Core.Abstractions;

using GestureClip.Core.Hotkeys;

public interface IHotkeyRegistrar
{
    event EventHandler? HotkeyPressed;

    bool RegisterOpenClipboardHotkey(HotkeyDefinition hotkey);

    void UnregisterOpenClipboardHotkey();

    int GetLastError();
}
