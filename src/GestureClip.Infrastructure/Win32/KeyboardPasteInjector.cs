using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace GestureClip.Infrastructure.Win32;

/// <summary>
/// Reliable synthetic Ctrl+V for gestures and hotkeys.
/// Keep the happy path simple: release stuck input → one-shot Ctrl+V.
/// Fallbacks only when SendInput reports incomplete delivery.
/// </summary>
public static partial class KeyboardPasteInjector
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
    private static readonly TimeSpan ModifierReleaseDelay = TimeSpan.FromMilliseconds(30);
    private static readonly TimeSpan InjectionCompletionDelay = TimeSpan.FromMilliseconds(15);

    public static ushort[] ModifierVirtualKeys { get; } =
    [
        VkLShift, VkRShift, VkShift,
        VkLControl, VkRControl, VkControl,
        VkLMenu, VkRMenu, VkMenu,
        VkLWin, VkRWin
    ];

    /// <summary>
    /// Preferred entry for paste injection.
    /// Does not re-activate the current foreground window (that can steal caret focus).
    /// An explicit target must still exist and own foreground focus immediately before injection.
    /// </summary>
    public static async Task<bool> SendCtrlVAsync(
        ILogger? logger = null,
        CancellationToken cancellationToken = default,
        IntPtr preferredTargetWindow = default,
        bool preferClipboardMessage = false)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _ = preferClipboardMessage; // reserved: all paths may end with WM_PASTE

        EnsurePreferredTarget(preferredTargetWindow);

        // Phase 1: release keyboard modifiers only (Shift stuck after Ctrl+Shift+V, etc.).
        // Never inject mouse button-ups: after right-gesture the LL hook already ate
        // physical down/up; synthetic RIGHTUP commonly pops the context menu.
        ReleaseKeyboardModifiers(logger);

        await Task.Delay(ModifierReleaseDelay, cancellationToken);

        // Phase 2: one-shot Ctrl+V (VK + scan). This is the path that works when smart paste is off.
        EnsurePreferredTarget(preferredTargetWindow);
        if (TrySendCtrlVViaSendInput(logger, preferredTargetWindow))
        {
            await Task.Delay(InjectionCompletionDelay, cancellationToken);
            return true;
        }

        // Phase 3: keybd_event fallback
        EnsurePreferredTarget(preferredTargetWindow);
        if (TrySendCtrlVViaKeybdEvent(logger))
        {
            await Task.Delay(InjectionCompletionDelay, cancellationToken);
            return true;
        }

        // Phase 4: WM_PASTE (classic edit controls)
        EnsurePreferredTarget(preferredTargetWindow);
        if (WindowNativeMethods.TryPostPasteMessage(preferredTargetWindow))
        {
            logger?.LogInformation("Paste recovered via WM_PASTE fallback.");
            await Task.Delay(InjectionCompletionDelay, cancellationToken);
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

    private static void ReleaseKeyboardModifiers(ILogger? logger)
    {
        var release = BuildModifierReleaseOnly();
        var released = Send(release);
        if (released != release.Length)
        {
            logger?.LogWarning(
                "Modifier release SendInput {Sent}/{Expected}, Win32={Win32}",
                released,
                release.Length,
                Marshal.GetLastWin32Error());
        }

    }

    private static void EnsurePreferredTarget(IntPtr preferredTargetWindow)
    {
        if (preferredTargetWindow == IntPtr.Zero) return;
        if (!WindowNativeMethods.IsWindow(preferredTargetWindow))
            throw new InvalidOperationException("粘贴目标窗口已关闭，已停止输入。");
        if (WindowNativeMethods.GetForegroundWindow() != preferredTargetWindow)
            throw new InvalidOperationException("粘贴目标已失去焦点，已停止输入；请返回目标应用后重试。");
    }
}
