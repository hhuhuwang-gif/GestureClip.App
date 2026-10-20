using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace GestureClip.Infrastructure.Win32;

public static partial class KeyboardPasteInjector
{
    private static bool TrySendCtrlVViaSendInput(ILogger? logger, IntPtr preferredTargetWindow)
    {
        var paste = BuildCtrlVChord(false);
        EnsurePreferredTarget(preferredTargetWindow);
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
        var scanPaste = BuildCtrlVChord(true);
        EnsurePreferredTarget(preferredTargetWindow);
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

}
