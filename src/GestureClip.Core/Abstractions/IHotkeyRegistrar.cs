namespace GestureClip.Core.Abstractions;

using GestureClip.Core.Hotkeys;

public interface IHotkeyRegistrar
{
    event EventHandler? HotkeyPressed;

    event EventHandler? QuickActionHotkeyPressed;

    bool RegisterOpenClipboardHotkey(HotkeyDefinition hotkey);

    void UnregisterOpenClipboardHotkey();

    bool RegisterOpenQuickActionHotkey(HotkeyDefinition hotkey);

    void UnregisterOpenQuickActionHotkey();

    int GetLastError();
}
