using System.Runtime.InteropServices;

namespace GestureClip.Infrastructure.Win32;

/// <summary>
/// Reliable synthetic Ctrl+V after global hotkeys / gestures.
/// Always releases stuck modifiers first (e.g. user still holding Shift after Ctrl+Shift+V).
/// </summary>
public static class KeyboardPasteInjector
{
    private const ushort VkShift = 0x10;
    private const ushort VkControl = 0x11;
    private const ushort VkMenu = 0x12; // Alt
    private const ushort VkLWin = 0x5B;
    private const ushort VkRWin = 0x5C;
    private const ushort VkLShift = 0xA0;
    private const ushort VkRShift = 0xA1;
    private const ushort VkLControl = 0xA2;
    private const ushort VkRControl = 0xA3;
    private const ushort VkLMenu = 0xA4;
    private const ushort VkRMenu = 0xA5;
    private const ushort VkV = 0x56;
    private const uint InputKeyboard = 1;
    private const uint KeyEventKeyUp = 0x0002;

    /// <summary>Build SendInput events: release all modifiers, then Ctrl+V.</summary>
    public static ClipboardNativeMethods.INPUT[] BuildPasteWithModifierRelease()
    {
        var list = new List<ClipboardNativeMethods.INPUT>(20);
        foreach (var vk in ModifierVirtualKeys)
        {
            list.Add(KeyUp(vk));
        }

        list.Add(KeyDown(VkControl));
        list.Add(KeyDown(VkV));
        list.Add(KeyUp(VkV));
        list.Add(KeyUp(VkControl));
        return list.ToArray();
    }

    /// <summary>Modifier key-ups only (for use before other shortcuts).</summary>
    public static ClipboardNativeMethods.INPUT[] BuildModifierReleaseOnly()
    {
        return ModifierVirtualKeys.Select(KeyUp).ToArray();
    }

    public static ushort[] ModifierVirtualKeys { get; } =
    [
        VkLShift, VkRShift, VkShift,
        VkLControl, VkRControl, VkControl,
        VkLMenu, VkRMenu, VkMenu,
        VkLWin, VkRWin
    ];

    public static int Send(ClipboardNativeMethods.INPUT[] inputs)
    {
        if (inputs.Length == 0)
        {
            return 0;
        }

        return (int)ClipboardNativeMethods.SendInput(
            (uint)inputs.Length,
            inputs,
            Marshal.SizeOf<ClipboardNativeMethods.INPUT>());
    }

    private static ClipboardNativeMethods.INPUT KeyDown(ushort virtualKey) => Keyboard(virtualKey, 0);

    private static ClipboardNativeMethods.INPUT KeyUp(ushort virtualKey) => Keyboard(virtualKey, KeyEventKeyUp);

    private static ClipboardNativeMethods.INPUT Keyboard(ushort virtualKey, uint flags)
    {
        return new ClipboardNativeMethods.INPUT
        {
            type = InputKeyboard,
            u = new ClipboardNativeMethods.InputUnion
            {
                ki = new ClipboardNativeMethods.KEYBDINPUT
                {
                    wVk = virtualKey,
                    dwFlags = flags
                }
            }
        };
    }
}
