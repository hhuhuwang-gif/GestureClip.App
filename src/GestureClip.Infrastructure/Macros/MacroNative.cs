using System.Runtime.InteropServices;

namespace GestureClip.Infrastructure.Macros;

internal static class MacroNative
{
    internal const int KeyboardHook = 13, MouseHook = 14;
    internal const uint KeyboardInjected = 0x10, MouseInjected = 1;
    internal const uint KeyDown = 0x100, KeyUp = 0x101, SysKeyDown = 0x104, SysKeyUp = 0x105;
    internal const uint Move = 0x200, LeftDown = 0x201, LeftUp = 0x202, RightDown = 0x204, RightUp = 0x205;
    internal const uint MiddleDown = 0x207, MiddleUp = 0x208, Wheel = 0x20A, XDown = 0x20B, XUp = 0x20C, HWheel = 0x20E;
    internal const uint F8 = 0x77, F9 = 0x78;
    internal const uint ReplayInputTag = 0x47434D52;
    internal delegate nint HookProc(int code, nint message, nint data);

    [DllImport("user32.dll", SetLastError = true)] internal static extern nint SetWindowsHookEx(int id, HookProc callback, nint module, uint thread);
    [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool UnhookWindowsHookEx(nint hook);
    [DllImport("user32.dll")] internal static extern nint CallNextHookEx(nint hook, int code, nint message, nint data);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] internal static extern nint GetModuleHandle(string? name);
    [DllImport("user32.dll")] internal static extern int GetSystemMetrics(int index);
    [DllImport("user32.dll")] internal static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll")] internal static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] internal static extern nint WindowFromPoint(NativePoint point);
    [DllImport("user32.dll")] internal static extern nint GetAncestor(nint window, uint flags);
    [StructLayout(LayoutKind.Sequential)] internal struct NativePoint { public int X, Y; }

    [StructLayout(LayoutKind.Sequential)]
    internal struct KeyboardData
    {
        public uint Key, Scan, Flags, Time;
        public nuint Extra;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct MouseData
    {
        public int X, Y;
        public uint Data, Flags, Time;
        public nuint Extra;
    }
}
