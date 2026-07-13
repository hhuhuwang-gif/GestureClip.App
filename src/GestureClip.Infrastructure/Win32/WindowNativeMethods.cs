using System.Runtime.InteropServices;

namespace GestureClip.Infrastructure.Win32;

public static class WindowNativeMethods
{
    public const int SwMinimize = 6;
    public const int SwRestore = 9;
    public const uint WmClose = 0x0010;
    public const uint WmPaste = 0x0302;

    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool IsWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool IsIconic(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool BringWindowToTop(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern IntPtr GetFocus();

    [DllImport("user32.dll")]
    public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("kernel32.dll")]
    public static extern uint GetCurrentThreadId();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

    [DllImport("user32.dll")]
    public static extern IntPtr SetFocus(IntPtr hWnd);

    /// <summary>
    /// Best-effort: bring <paramref name="hwnd"/> to the foreground so subsequent
    /// SendInput / WM_PASTE lands in the target app (critical after overlays / gestures).
    /// </summary>
    public static bool TryActivateWindow(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero || !IsWindow(hwnd))
        {
            return false;
        }

        if (GetForegroundWindow() == hwnd)
        {
            return true;
        }

        if (IsIconic(hwnd))
        {
            ShowWindow(hwnd, SwRestore);
        }

        var foreground = GetForegroundWindow();
        var currentThreadId = GetCurrentThreadId();
        var foregroundThreadId = foreground == IntPtr.Zero
            ? 0u
            : GetWindowThreadProcessId(foreground, out _);
        var targetThreadId = GetWindowThreadProcessId(hwnd, out _);

        var attachedFg = false;
        var attachedTarget = false;
        try
        {
            if (foregroundThreadId != 0 && foregroundThreadId != currentThreadId)
            {
                attachedFg = AttachThreadInput(currentThreadId, foregroundThreadId, true);
            }

            if (targetThreadId != 0 && targetThreadId != currentThreadId && targetThreadId != foregroundThreadId)
            {
                attachedTarget = AttachThreadInput(currentThreadId, targetThreadId, true);
            }

            BringWindowToTop(hwnd);
            SetForegroundWindow(hwnd);
            return GetForegroundWindow() == hwnd;
        }
        finally
        {
            if (attachedTarget)
            {
                AttachThreadInput(currentThreadId, targetThreadId, false);
            }

            if (attachedFg)
            {
                AttachThreadInput(currentThreadId, foregroundThreadId, false);
            }
        }
    }

    /// <summary>
    /// Post WM_PASTE to the focused control (or foreground window). Reliable when
    /// synthetic Ctrl+V is swallowed (UIPI partial, Electron quirks, residual mouse state).
    /// </summary>
    public static bool TryPostPasteMessage(IntPtr preferredHwnd = default)
    {
        var target = ResolvePasteTarget(preferredHwnd);
        if (target == IntPtr.Zero)
        {
            return false;
        }

        // PostMessage returns nonzero on success (message queued).
        return PostMessage(target, WmPaste, IntPtr.Zero, IntPtr.Zero) != IntPtr.Zero;
    }

    public static IntPtr ResolvePasteTarget(IntPtr preferredHwnd = default)
    {
        if (preferredHwnd != IntPtr.Zero && IsWindow(preferredHwnd))
        {
            TryActivateWindow(preferredHwnd);
        }

        var foreground = GetForegroundWindow();
        if (foreground == IntPtr.Zero)
        {
            return preferredHwnd;
        }

        var currentThreadId = GetCurrentThreadId();
        var foregroundThreadId = GetWindowThreadProcessId(foreground, out _);
        var attached = false;
        try
        {
            if (foregroundThreadId != 0 && foregroundThreadId != currentThreadId)
            {
                attached = AttachThreadInput(currentThreadId, foregroundThreadId, true);
            }

            var focus = GetFocus();
            if (focus != IntPtr.Zero && IsWindow(focus))
            {
                return focus;
            }
        }
        finally
        {
            if (attached)
            {
                AttachThreadInput(currentThreadId, foregroundThreadId, false);
            }
        }

        return foreground;
    }
}
