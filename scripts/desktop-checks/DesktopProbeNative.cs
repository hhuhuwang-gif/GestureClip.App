using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

public static class DesktopProbeNative
{
    private const uint KeyboardInput = 1;
    private const uint KeyUp = 2;
    private const uint RightDown = 8;
    private const uint RightUp = 16;
    private const int GestureLength = 140;
    private const int GestureSteps = 14;
    private const int GestureStepMilliseconds = 8;
    [StructLayout(LayoutKind.Sequential)] public struct Point { public int X; public int Y; }
    [StructLayout(LayoutKind.Sequential)] private struct Rect { public int Left; public int Top; public int Right; public int Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct MouseInput { public int X; public int Y; public uint Data; public uint Flags; public uint Time; public IntPtr Extra; }
    [StructLayout(LayoutKind.Sequential)] private struct KeyInput { public ushort Key; public ushort Scan; public uint Flags; public uint Time; public IntPtr Extra; }
    [StructLayout(LayoutKind.Explicit)] private struct InputUnion { [FieldOffset(0)] public MouseInput Mouse; [FieldOffset(0)] public KeyInput Key; }
    [StructLayout(LayoutKind.Sequential)] private struct Input { public uint Type; public InputUnion Data; }
    private delegate bool WindowCallback(IntPtr window, IntPtr parameter);
    [DllImport("user32.dll", SetLastError = true)] private static extern uint SendInput(uint count, Input[] inputs, int size);
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr window);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll")] public static extern IntPtr GetAncestor(IntPtr window, uint flags);
    [DllImport("user32.dll")] private static extern bool EnumWindows(WindowCallback callback, IntPtr parameter);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(IntPtr window, StringBuilder text, int size);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr window, out Rect rectangle);
    [DllImport("user32.dll")] public static extern bool GetCursorPos(out Point point);
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);

    public static int ProcessId(IntPtr window)
    {
        uint processId;
        GetWindowThreadProcessId(window, out processId);
        return checked((int)processId);
    }

    public static IntPtr FindVisible(int processId, string title)
    {
        IntPtr found = IntPtr.Zero;
        EnumWindows(delegate(IntPtr window, IntPtr parameter)
        {
            if (!IsWindowVisible(window) || (processId != 0 && ProcessId(window) != processId)) return true;
            var text = new StringBuilder(1024);
            GetWindowText(window, text, text.Capacity);
            if (!text.ToString().StartsWith(title, StringComparison.Ordinal)) return true;
            found = window;
            return false;
        }, IntPtr.Zero);
        return found;
    }

    public static void Activate(IntPtr window)
    {
        if (!IsWindowVisible(window)) throw new InvalidOperationException("Probe target is not visible.");
        SetForegroundWindow(window);
        if (GetForegroundWindow() != window) throw new InvalidOperationException("Probe could not acquire target focus.");
    }

    private static void Send(Input input)
    {
        if (SendInput(1, new[] { input }, Marshal.SizeOf(typeof(Input))) != 1)
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Probe input was not delivered.");
    }

    private static void Key(ushort key, bool release)
    {
        Send(new Input { Type = KeyboardInput, Data = new InputUnion { Key = new KeyInput { Key = key, Flags = release ? KeyUp : 0 } } });
    }

    public static Stopwatch Chord(ushort modifier, ushort key)
    {
        var watch = Stopwatch.StartNew();
        if (modifier != 0) Key(modifier, false);
        try { Key(key, false); Key(key, true); }
        finally { if (modifier != 0) Key(modifier, true); }
        return watch;
    }

    public static Stopwatch DownGesture(IntPtr window)
    {
        Activate(window);
        Rect rectangle;
        if (!GetWindowRect(window, out rectangle)) throw new InvalidOperationException("Cannot locate probe target.");
        var centerX = (rectangle.Left + rectangle.Right) / 2;
        var centerY = (rectangle.Top + rectangle.Bottom) / 2;
        if (!SetCursorPos(centerX, centerY)) throw new InvalidOperationException("Cannot position probe pointer.");
        Send(new Input { Data = new InputUnion { Mouse = new MouseInput { Flags = RightDown } } });
        var watch = new Stopwatch();
        try
        {
            for (var step = 1; step <= GestureSteps; step++)
            {
                if (!SetCursorPos(centerX, centerY + GestureLength * step / GestureSteps))
                    throw new InvalidOperationException("Cannot trace probe gesture.");
                Thread.Sleep(GestureStepMilliseconds);
            }
        }
        finally
        {
            watch.Start();
            Send(new Input { Data = new InputUnion { Mouse = new MouseInput { Flags = RightUp } } });
        }
        return watch;
    }
}
