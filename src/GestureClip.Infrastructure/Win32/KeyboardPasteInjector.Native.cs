using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace GestureClip.Infrastructure.Win32;

public static partial class KeyboardPasteInjector
{
    private static ClipboardNativeMethods.INPUT ToClipboardInput(KeyboardInputNativeMethods.INPUT src)
    {
        return new ClipboardNativeMethods.INPUT
        {
            type = src.type,
            u = new ClipboardNativeMethods.InputUnion
            {
                ki = new ClipboardNativeMethods.KEYBDINPUT
                {
                    wVk = src.u.ki.wVk,
                    wScan = src.u.ki.wScan,
                    dwFlags = src.u.ki.dwFlags,
                    time = src.u.ki.time,
                    dwExtraInfo = src.u.ki.dwExtraInfo
                }
            }
        };
    }

    private static KeyboardInputNativeMethods.INPUT KeyEvent(ushort virtualKey, uint flags)
    {
        var scan = (ushort)MapVirtualKey(virtualKey, MapVkToVsc);
        return new KeyboardInputNativeMethods.INPUT
        {
            type = InputKeyboard,
            u = new KeyboardInputNativeMethods.InputUnion
            {
                ki = new KeyboardInputNativeMethods.KEYBDINPUT
                {
                    wVk = virtualKey,
                    wScan = scan,
                    dwFlags = flags,
                    time = 0,
                    dwExtraInfo = 0
                }
            }
        };
    }

    private static KeyboardInputNativeMethods.INPUT ScanKeyEvent(ushort virtualKey, uint flags)
    {
        var scan = (ushort)MapVirtualKey(virtualKey, MapVkToVsc);
        return new KeyboardInputNativeMethods.INPUT
        {
            type = InputKeyboard,
            u = new KeyboardInputNativeMethods.InputUnion
            {
                ki = new KeyboardInputNativeMethods.KEYBDINPUT
                {
                    wVk = 0,
                    wScan = scan,
                    dwFlags = flags | KeyEventScancode,
                    time = 0,
                    dwExtraInfo = 0
                }
            }
        };
    }

    private static KeyboardInputNativeMethods.INPUT MouseUp(uint flags, uint mouseData)
    {
        return new KeyboardInputNativeMethods.INPUT
        {
            type = InputMouse,
            u = new KeyboardInputNativeMethods.InputUnion
            {
                mi = new KeyboardInputNativeMethods.MOUSEINPUT
                {
                    dx = 0,
                    dy = 0,
                    mouseData = mouseData,
                    dwFlags = flags,
                    time = 0,
                    dwExtraInfo = 0
                }
            }
        };
    }

    [DllImport("user32.dll")]
    private static extern uint MapVirtualKey(uint uCode, uint uMapType);

    [DllImport("user32.dll")]
    private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

}
