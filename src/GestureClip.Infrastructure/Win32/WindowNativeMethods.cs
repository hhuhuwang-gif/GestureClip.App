using System.Runtime.InteropServices;

namespace GestureClip.Infrastructure.Win32;

public static class WindowNativeMethods
{
    public const int SwMinimize = 6;
    public const uint WmClose = 0x0010;

    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
}
