namespace GestureClip.Core.Abstractions;

using GestureClip.Core.Hotkeys;

public interface IHotkeyRegistrar
{
    event EventHandler? HotkeyPressed;

    event EventHandler? QuickActionHotkeyPressed;

    event EventHandler? PastePlainTextHotkeyPressed;

    event EventHandler? PasteQueueHotkeyPressed;

    bool RegisterOpenClipboardHotkey(HotkeyDefinition hotkey);

    void UnregisterOpenClipboardHotkey();

    bool RegisterOpenQuickActionHotkey(HotkeyDefinition hotkey);

    void UnregisterOpenQuickActionHotkey();

    bool RegisterPastePlainTextHotkey(HotkeyDefinition hotkey);

    void UnregisterPastePlainTextHotkey();

    bool RegisterPasteQueueHotkey(HotkeyDefinition hotkey);

    void UnregisterPasteQueueHotkey();

    int GetLastError();
}
