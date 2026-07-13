using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace GestureClip.Infrastructure.Win32;

/// <summary>
/// Reliable synthetic paste for gestures and hotkeys across machines.
/// Hardening for common failures:
/// 1) Wrong INPUT struct size on x64 (union must include MOUSEINPUT)
/// 2) Stuck keyboard modifiers after hotkeys
/// 3) Stuck mouse buttons after right-button gestures
/// 4) Missing scan codes rejected by some apps
/// 5) Focus lost after overlays → paste lands nowhere
/// 6) SendInput silently ineffective → keybd_event + WM_PASTE fallbacks
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

    /// <summary>
    /// Full paste sequence: mouse button ups → modifier key ups → Ctrl+V (with scan codes).
    /// Uses <see cref="KeyboardInputNativeMethods.INPUT"/> for correct x64 layout.
    /// </summary>
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
    /// Preferred entry: multi-path paste with focus restore.
    /// Path order: release → activate → SendInput Ctrl+V → keybd_event → WM_PASTE.
    /// </summary>
    public static async Task<bool> SendCtrlVAsync(
        ILogger? logger = null,
        CancellationToken cancellationToken = default,
        IntPtr preferredTargetWindow = default,
        bool preferClipboardMessage = false)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Phase 0: restore target focus early (overlays / HUD must not own input).
        if (preferredTargetWindow != IntPtr.Zero)
        {
            var activated = WindowNativeMethods.TryActivateWindow(preferredTargetWindow);
            logger?.LogDebug("Paste focus restore preferredHwnd={Hwnd} ok={Ok}", preferredTargetWindow, activated);
        }
        else
        {
            // Soft re-assert current foreground (helps after non-activating topmost HUD).
            var fg = WindowNativeMethods.GetForegroundWindow();
            if (fg != IntPtr.Zero)
            {
                WindowNativeMethods.TryActivateWindow(fg);
            }
        }

        // Phase 1: release mouse + keyboard modifiers only
        var release = new List<KeyboardInputNativeMethods.INPUT>();
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

        await Task.Delay(50, cancellationToken);

        // Re-assert focus after synthetic mouse ups (some apps re-focus on mouse change).
        if (preferredTargetWindow != IntPtr.Zero)
        {
            WindowNativeMethods.TryActivateWindow(preferredTargetWindow);
            await Task.Delay(25, cancellationToken);
        }
        else
        {
            var fg = WindowNativeMethods.GetForegroundWindow();
            if (fg != IntPtr.Zero)
            {
                WindowNativeMethods.TryActivateWindow(fg);
            }
        }

        // Phase 2: SendInput Ctrl+V (split Ctrl down / V for apps that drop chorded synthetic input)
        if (await TrySendCtrlVViaSendInputSplitAsync(logger, cancellationToken))
        {
            await Task.Delay(25, cancellationToken);
            // When clipboard was just rewritten, also fire WM_PASTE as a no-harm recovery for
            // hosts that accept SendInput count but drop the chord (rare). Skip if not preferred
            // to avoid double-paste in hosts that honor both.
            if (preferClipboardMessage)
            {
                // Only as soft secondary when primary keyboard path ran — many edit controls
                // ignore a second paste if selection already replaced; risk accepted for rewrite path.
                // Actually double-paste is bad. Do NOT dual-fire.
            }

            return true;
        }

        // Phase 3: keybd_event fallback
        if (TrySendCtrlVViaKeybdEvent(logger))
        {
            await Task.Delay(25, cancellationToken);
            return true;
        }

        // Phase 4: WM_PASTE to focused control / preferred window (best for classic Win32 edits)
        if (WindowNativeMethods.TryPostPasteMessage(preferredTargetWindow))
        {
            logger?.LogInformation("Paste recovered via WM_PASTE fallback.");
            await Task.Delay(20, cancellationToken);
            return true;
        }

        // Phase 5: last try — classic single-batch SendInput
        if (TrySendCtrlVViaSendInput(logger))
        {
            await Task.Delay(20, cancellationToken);
            return true;
        }

        logger?.LogWarning("All paste injection paths failed (SendInput, keybd_event, WM_PASTE).");
        return false;
    }

    /// <summary>Synchronous multi-path paste for callers that cannot await.</summary>
    public static bool SendCtrlV(
        ILogger? logger = null,
        IntPtr preferredTargetWindow = default,
        bool preferClipboardMessage = false)
    {
        return SendCtrlVAsync(logger, CancellationToken.None, preferredTargetWindow, preferClipboardMessage)
            .GetAwaiter()
            .GetResult();
    }

    // --- Legacy API used by older tests / ClipboardNativeMethods path ---

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

    private static async Task<bool> TrySendCtrlVViaSendInputSplitAsync(
        ILogger? logger,
        CancellationToken cancellationToken)
    {
        // Ctrl down first, brief gap, then V down/up + Ctrl up — more reliable after right-button gestures.
        var ctrlDown = new[] { KeyEvent(VkControl, 0) };
        var sent = Send(ctrlDown);
        if (sent != 1)
        {
            logger?.LogWarning(
                "Ctrl-down SendInput {Sent}/1, Win32={Win32}",
                sent,
                Marshal.GetLastWin32Error());
            return false;
        }

        await Task.Delay(18, cancellationToken);

        var rest =
            new[]
            {
                KeyEvent(VkV, 0),
                KeyEvent(VkV, KeyEventKeyUp),
                KeyEvent(VkControl, KeyEventKeyUp)
            };
        sent = Send(rest);
        if (sent == rest.Length)
        {
            return true;
        }

        // Ensure Ctrl is not left stuck.
        _ = Send([KeyEvent(VkControl, KeyEventKeyUp), KeyEvent(VkLControl, KeyEventKeyUp), KeyEvent(VkRControl, KeyEventKeyUp)]);
        logger?.LogWarning(
            "Ctrl+V tail SendInput {Sent}/{Expected}, Win32={Win32}",
            sent,
            rest.Length,
            Marshal.GetLastWin32Error());
        return false;
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

        // Second attempt: pure scan-code keys (some hosts reject VK-only synthesis).
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
            // Release common modifiers first.
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
