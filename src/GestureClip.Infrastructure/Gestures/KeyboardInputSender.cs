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

        // Release stuck modifiers first so Ctrl+V is not polluted by still-held Shift/Alt.
        var release = KeyboardPasteInjector.BuildModifierReleaseOnly();
        _ = KeyboardPasteInjector.Send(release);

        var inputs = new List<KeyboardInputNativeMethods.INPUT>();
        inputs.AddRange(keys.Select(key => KeyboardInput(key, 0)));
        inputs.AddRange(keys.Reverse().Select(key => KeyboardInput(key, KeyboardInputNativeMethods.KeyEventKeyUp)));
        var sent = KeyboardInputNativeMethods.SendInput((uint)inputs.Count, [.. inputs], Marshal.SizeOf<KeyboardInputNativeMethods.INPUT>());
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
                    dwFlags = flags
                }
            }
        };
    }
}
