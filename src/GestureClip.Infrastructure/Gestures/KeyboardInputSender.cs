using System.Runtime.InteropServices;
using GestureClip.Core.Abstractions;
using GestureClip.Infrastructure.Win32;
using Microsoft.Extensions.Logging;

namespace GestureClip.Infrastructure.Gestures;

public sealed class KeyboardInputSender : IKeyboardInputSender
{
    private readonly ILogger<KeyboardInputSender> _logger;
    private string? _lastStatus;

    public KeyboardInputSender(ILogger<KeyboardInputSender> logger)
    {
        _logger = logger;
    }

    public string? LastStatus => _lastStatus;

    public void SendShortcut(params ushort[] keys)
    {
        if (keys.Length == 0)
        {
            _lastStatus = "No keyboard input sent.";
            return;
        }

        // Fast path for Ctrl+V: multi-path hardened injector (focus + SendInput + keybd + WM_PASTE).
        if (keys.Length == 2 &&
            keys[0] == KeyboardInputNativeMethods.VkControl &&
            keys[1] == KeyboardInputNativeMethods.VkV)
        {
            try
            {
                // Clipboard already holds content for paste; enable WM_PASTE fallback.
                var ok = KeyboardPasteInjector.SendCtrlV(
                    _logger,
                    preferredTargetWindow: default,
                    preferClipboardMessage: true);
                _lastStatus = ok
                    ? "Sent Ctrl+V via KeyboardPasteInjector"
                    : "Ctrl+V multi-path injection reported failure";
                if (!ok)
                {
                    _logger.LogWarning("Ctrl+V multi-path injection reported failure.");
                }
            }
            catch (Exception ex)
            {
                _lastStatus = $"Ctrl+V failed: {ex.Message}";
                _logger.LogWarning(ex, "Ctrl+V via KeyboardPasteInjector failed.");
            }

            return;
        }

        // Release keyboard modifiers only (no synthetic mouse ups — avoids context menu).
        _ = KeyboardPasteInjector.Send(KeyboardPasteInjector.BuildModifierReleaseOnly());

        var inputs = new List<KeyboardInputNativeMethods.INPUT>();
        inputs.AddRange(keys.Select(key => KeyboardInput(key, 0)));
        inputs.AddRange(keys.Reverse().Select(key => KeyboardInput(key, KeyboardInputNativeMethods.KeyEventKeyUp)));
        var sent = KeyboardInputNativeMethods.SendInput(
            (uint)inputs.Count,
            [.. inputs],
            Marshal.SizeOf<KeyboardInputNativeMethods.INPUT>());
        if (sent != inputs.Count)
        {
            var error = Marshal.GetLastWin32Error();
            _lastStatus = $"Sent {sent}/{inputs.Count}; Win32Error={error}";
            _logger.LogWarning(
                "Keyboard SendInput sent {Sent}/{Expected} events. Win32Error={Win32Error}",
                sent,
                inputs.Count,
                error);
            return;
        }

        _lastStatus = $"Sent {sent}/{inputs.Count}";
    }

    public void SendKey(ushort key)
    {
        SendShortcut(key);
    }

    private static KeyboardInputNativeMethods.INPUT KeyboardInput(ushort virtualKey, uint flags)
    {
        return new KeyboardInputNativeMethods.INPUT
        {
            type = KeyboardInputNativeMethods.InputKeyboard,
            u = new KeyboardInputNativeMethods.InputUnion
            {
                ki = new KeyboardInputNativeMethods.KEYBDINPUT
                {
                    wVk = virtualKey,
                    wScan = 0,
                    dwFlags = flags
                }
            }
        };
    }
}
