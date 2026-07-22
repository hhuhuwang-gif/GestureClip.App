using System.Runtime.InteropServices;
using GestureClip.Core.Abstractions;
using GestureClip.Infrastructure.Win32;
using Microsoft.Extensions.Logging;

namespace GestureClip.Infrastructure.Gestures;

public sealed class KeyboardInputSender : IKeyboardInputSender
{
    private readonly ILogger<KeyboardInputSender> _logger;
    private string? _lastStatus;
    private string? _lastPasteFailureHint;

    public KeyboardInputSender(ILogger<KeyboardInputSender> logger)
    {
        _logger = logger;
    }

    public string? LastStatus => _lastStatus;

    public string? TryGetLastPasteFailureHint() => _lastPasteFailureHint;

    public async Task<bool> SendPasteAsync(nint preferredTargetWindow = 0, CancellationToken cancellationToken = default)
    {
        _lastPasteFailureHint = null;
        try
        {
            var ok = await KeyboardPasteInjector.SendCtrlVAsync(
                _logger,
                cancellationToken,
                preferredTargetWindow: preferredTargetWindow,
                preferClipboardMessage: true);
            _lastStatus = ok
                ? preferredTargetWindow == 0
                    ? "Sent Ctrl+V via KeyboardPasteInjector"
                    : $"Sent Ctrl+V via KeyboardPasteInjector (target={preferredTargetWindow})"
                : "Ctrl+V multi-path injection reported failure";

            if (!ok)
            {
                _lastPasteFailureHint = BuildPasteFailureHint(preferredTargetWindow);
                _logger.LogWarning(
                    "Ctrl+V multi-path injection reported failure. Hint={Hint}",
                    _lastPasteFailureHint);
            }

            return ok;
        }
        catch (Exception ex)
        {
            _lastStatus = $"Ctrl+V failed: {ex.Message}";
            _lastPasteFailureHint = BuildPasteFailureHint(preferredTargetWindow);
            _logger.LogWarning(ex, "Ctrl+V via KeyboardPasteInjector failed.");
            return false;
        }
    }

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
            // Prefer SendPasteAsync so callers can await and pass a target window.
            // Keep sync path for non-paste shortcuts that accidentally use this overload.
            try
            {
                var ok = KeyboardPasteInjector.SendCtrlV(
                    _logger,
                    preferredTargetWindow: default,
                    preferClipboardMessage: true);
                _lastStatus = ok
                    ? "Sent Ctrl+V via KeyboardPasteInjector"
                    : "Ctrl+V multi-path injection reported failure";
                if (!ok)
                {
                    _lastPasteFailureHint = BuildPasteFailureHint(0);
                    _logger.LogWarning("Ctrl+V multi-path injection reported failure.");
                }
            }
            catch (Exception ex)
            {
                _lastStatus = $"Ctrl+V failed: {ex.Message}";
                _lastPasteFailureHint = BuildPasteFailureHint(0);
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

    private static string BuildPasteFailureHint(nint preferredTargetWindow)
    {
        // UIPI: non-elevated GestureClip cannot inject into elevated/admin windows.
        if (!IsCurrentProcessElevated())
        {
            return "粘贴可能失败：目标窗口权限更高（管理员）。请用管理员身份重新启动 GestureClip，或在目标窗口内手动 Ctrl+V。";
        }

        if (preferredTargetWindow != 0)
        {
            return "粘贴可能失败：目标窗口未接收到输入。请点击目标输入框后再试一次下滑粘贴。";
        }

        return "粘贴可能失败：请确认光标在可输入区域，或改用历史面板里的粘贴。";
    }

    private static bool IsCurrentProcessElevated()
    {
        try
        {
            using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
            var principal = new System.Security.Principal.WindowsPrincipal(identity);
            return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
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
