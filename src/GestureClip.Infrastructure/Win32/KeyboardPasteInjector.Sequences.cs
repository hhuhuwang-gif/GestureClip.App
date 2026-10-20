using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace GestureClip.Infrastructure.Win32;

public static partial class KeyboardPasteInjector
{
    private static KeyboardInputNativeMethods.INPUT[] BuildCtrlVChord(bool scanCodes)
    {
        Func<ushort, uint, KeyboardInputNativeMethods.INPUT> keyEvent = scanCodes ? ScanKeyEvent : KeyEvent;
        return [keyEvent(VkControl, 0), keyEvent(VkV, 0),
            keyEvent(VkV, KeyEventKeyUp), keyEvent(VkControl, KeyEventKeyUp)];
    }

    public static KeyboardInputNativeMethods.INPUT[] BuildFullPasteSequence()
    {
        // Keyboard only. Do NOT inject synthetic mouse button-ups here:
        // after a right-button gesture (down/up already eaten by the LL hook), a
        // synthetic RIGHTUP often reaches the app and opens the context menu.
        var list = new List<KeyboardInputNativeMethods.INPUT>(16);
        foreach (var vk in ModifierVirtualKeys)
        {
            list.Add(KeyEvent(vk, KeyEventKeyUp));
        }

        list.Add(KeyEvent(VkControl, 0));
        list.Add(KeyEvent(VkV, 0));
        list.Add(KeyEvent(VkV, KeyEventKeyUp));
        list.Add(KeyEvent(VkControl, KeyEventKeyUp));
        return list.ToArray();
    }

    public static KeyboardInputNativeMethods.INPUT[] BuildModifierReleaseOnly()
    {
        return ModifierVirtualKeys.Select(vk => KeyEvent(vk, KeyEventKeyUp)).ToArray();
    }

    public static KeyboardInputNativeMethods.INPUT[] BuildMouseButtonUps()
    {
        return
        [
            MouseUp(MouseEventLeftUp, 0),
            MouseUp(MouseEventRightUp, 0),
            MouseUp(MouseEventMiddleUp, 0),
            MouseUp(MouseEventXUp, XButton1),
            MouseUp(MouseEventXUp, XButton2)
        ];
    }

    public static int Send(KeyboardInputNativeMethods.INPUT[] inputs)
    {
        if (inputs.Length == 0)
        {
            return 0;
        }

        return (int)KeyboardInputNativeMethods.SendInput(
            (uint)inputs.Length,
            inputs,
            Marshal.SizeOf<KeyboardInputNativeMethods.INPUT>());
    }

}
