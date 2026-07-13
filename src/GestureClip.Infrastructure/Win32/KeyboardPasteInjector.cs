using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace GestureClip.Infrastructure.Win32;

/// <summary>
/// Reliable synthetic Ctrl+V for gestures and hotkeys.
/// Keep the happy path simple: release stuck input → one-shot Ctrl+V.
/// Fallbacks only when SendInput reports incomplete delivery.
/// </summary>
public static class KeyboardPasteInjector
{
    private const ushort VkShift = 0x10;
    private const ushort VkControl = 0x11;
    private const ushort VkMenu = 0x12;
    private const ushort VkLWin = 0x5B;
    private const ushort VkRWin = 0x5C;
    private const ushort VkLShift = 0xA0;
    private const ushort VkRShift = 0xA1;
    private const ushort VkLControl = 0xA2;
    private const ushort VkRControl = 0xA3;
    private const ushort VkLMenu = 0xA4;
    private const ushort VkRMenu = 0xA5;
    private const ushort VkV = 0x56;

    private const uint InputMouse = 0;
    private const uint InputKeyboard = 1;
    private const uint KeyEventKeyUp = 0x0002;
    private const uint KeyEventScancode = 0x0008;
    private const uint MouseEventLeftUp = 0x0004;
    private const uint MouseEventRightUp = 0x0010;
    private const uint MouseEventMiddleUp = 0x0040;
    private const uint MouseEventXUp = 0x0100;
    private const uint XButton1 = 0x0001;
    private const uint XButton2 = 0x0002;
    private const uint MapVkToVsc = 0;

    public static ushort[] ModifierVirtualKeys { get; } =
    [
        VkLShift, VkRShift, VkShift,
        VkLControl, VkRControl, VkControl,
        VkLMenu, VkRMenu, VkMenu,
        VkLWin, VkRWin
    ];

    public static KeyboardInputNativeMethods.INPUT[] BuildFullPasteSequence()
    {
        var list = new List<KeyboardInputNativeMethods.INPUT>(32);
        list.AddRange(BuildMouseButtonUps());
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

    /// <summary>
    /// Preferred entry for paste injection.
    /// Does not re-activate the current foreground window (that can steal caret focus).
    /// Only activates when an explicit preferred target is provided and it is not already FG.
    /// </summary>
    public static async Task<bool> SendCtrlVAsync(
        ILogger? logger = null,
        CancellationToken cancellationToken = default,
        IntPtr preferredTargetWindow = default,
        bool preferClipboardMessage = false)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _ = preferClipboardMessage; // reserved: all paths may end with WM_PASTE

        if (preferredTargetWindow != IntPtr.Zero &&
            WindowNativeMethods.IsWindow(preferredTargetWindow) &&
            WindowNativeMethods.GetForegroundWindow() != preferredTargetWindow)
        {
            var activated = WindowNativeMethods.TryActivateWindow(preferredTargetWindow);
            logger?.LogDebug("Paste activate preferredHwnd={Hwnd} ok={Ok}", preferredTargetWindow, activated);
            await Task.Delay(20, cancellationToken);
        }

        // Phase 1: clear stuck mouse buttons + modifiers after right-button gesture / hotkeys.
        var release = new List<KeyboardInputNativeMethods.INPUT>(16);
        release.AddRange(BuildMouseButtonUps());
        release.AddRange(BuildModifierReleaseOnly());
        var released = Send(release.ToArray());
        if (released != release.Count)
        {
            logger?.LogWarning(
                "Modifier/mouse release SendInput {Sent}/{Expected}, Win32={Win32}",
                released,
                release.Count,
                Marshal.GetLastWin32Error());
        }

        await Task.Delay(30, cancellationToken);

        // Phase 2: one-shot Ctrl+V (VK + scan). This is the path that works when smart paste is off.
        if (TrySendCtrlVViaSendInput(logger))
        {
            await Task.Delay(15, cancellationToken);
            return true;
        }

        // Phase 3: keybd_event fallback
        if (TrySendCtrlVViaKeybdEvent(logger))
        {
            await Task.Delay(15, cancellationToken);
            return true;
        }

        // Phase 4: WM_PASTE (classic edit controls)
        if (WindowNativeMethods.TryPostPasteMessage(preferredTargetWindow))
        {
            logger?.LogInformation("Paste recovered via WM_PASTE fallback.");
            await Task.Delay(15, cancellationToken);
            return true;
        }

        logger?.LogWarning("All paste injection paths failed (SendInput, keybd_event, WM_PASTE).");
        return false;
    }

    public static bool SendCtrlV(
        ILogger? logger = null,
        IntPtr preferredTargetWindow = default,
        bool preferClipboardMessage = false)
    {
        return SendCtrlVAsync(logger, CancellationToken.None, preferredTargetWindow, preferClipboardMessage)
            .GetAwaiter()
            .GetResult();
    }

    public static ClipboardNativeMethods.INPUT[] BuildPasteWithModifierRelease()
    {
        var modern = BuildFullPasteSequence();
        return modern.Select(ToClipboardInput).ToArray();
    }

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

    private static bool TrySendCtrlVViaSendInput(ILogger? logger)
    {
        var paste =
            new[]
            {
                KeyEvent(VkControl, 0),
                KeyEvent(VkV, 0),
                KeyEvent(VkV, KeyEventKeyUp),
                KeyEvent(VkControl, KeyEventKeyUp)
            };
        var sent = Send(paste);
        if (sent == paste.Length)
        {
            return true;
        }

        logger?.LogWarning(
            "Ctrl+V SendInput {Sent}/{Expected}, Win32={Win32}",
            sent,
            paste.Length,
            Marshal.GetLastWin32Error());

        // Ensure Ctrl is not left down if partial delivery happened.
        _ = Send(
        [
            KeyEvent(VkControl, KeyEventKeyUp),
            KeyEvent(VkLControl, KeyEventKeyUp),
            KeyEvent(VkRControl, KeyEventKeyUp)
        ]);

        // Scan-code-only retry.
        var scanPaste =
            new[]
            {
                ScanKeyEvent(VkControl, 0),
                ScanKeyEvent(VkV, 0),
                ScanKeyEvent(VkV, KeyEventKeyUp),
                ScanKeyEvent(VkControl, KeyEventKeyUp)
            };
        sent = Send(scanPaste);
        if (sent == scanPaste.Length)
        {
            logger?.LogDebug("Ctrl+V via KEYEVENTF_SCANCODE succeeded.");
            return true;
        }

        logger?.LogWarning(
            "Ctrl+V scan-code SendInput {Sent}/{Expected}, Win32={Win32}",
            sent,
            scanPaste.Length,
            Marshal.GetLastWin32Error());
        return false;
    }

    private static bool TrySendCtrlVViaKeybdEvent(ILogger? logger)
    {
        try
        {
            foreach (var vk in ModifierVirtualKeys)
            {
                keybd_event((byte)vk, 0, KeyEventKeyUp, UIntPtr.Zero);
            }

            keybd_event((byte)VkControl, 0, 0, UIntPtr.Zero);
            keybd_event((byte)VkV, 0, 0, UIntPtr.Zero);
            keybd_event((byte)VkV, 0, KeyEventKeyUp, UIntPtr.Zero);
            keybd_event((byte)VkControl, 0, KeyEventKeyUp, UIntPtr.Zero);
            logger?.LogDebug("Ctrl+V via keybd_event completed.");
            return true;
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "keybd_event Ctrl+V failed.");
            return false;
        }
    }

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
