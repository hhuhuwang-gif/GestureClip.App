using System.Diagnostics;
using System.Runtime.InteropServices;
using GestureClip.Core.Abstractions;
using GestureClip.Core.SystemInfo;
using GestureClip.Infrastructure.Win32;

namespace GestureClip.Infrastructure.SystemInfo;

public sealed class WindowsPasteTargetPlatform : IPasteTargetPlatform, IDisposable
{
    private const uint ForegroundChangedEvent = 3;
    private const uint SkipOwnProcess = 2;
    private const int PressedKeyMask = 0x8000;
    private const int EnterKey = 0x0D;
    private const int ShiftKey = 0x10;
    private const int ControlKey = 0x11;
    private const int AltKey = 0x12;
    private const int LeftWindowsKey = 0x5B;
    private const int RightWindowsKey = 0x5C;
    private const int PasteKey = 0x56;
    private static readonly int[] PasteKeys = [EnterKey, PasteKey, ShiftKey, ControlKey, AltKey, LeftWindowsKey, RightWindowsKey];
    private readonly ForegroundEventCallback _callback;
    private readonly nint _hook;
    private nint _lastExternalWindow;

    public WindowsPasteTargetPlatform()
    {
        _callback = OnForegroundChanged;
        _hook = SetWinEventHook(ForegroundChangedEvent, ForegroundChangedEvent, 0, _callback, 0, 0, SkipOwnProcess);
        if (_hook == 0) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        RememberExternalWindow(ForegroundWindow);
    }

    public nint ForegroundWindow => WindowNativeMethods.GetForegroundWindow();

    public bool ArePasteKeysReleased => PasteKeys.All(key => (GetAsyncKeyState(key) & PressedKeyMask) == 0);

    public bool IsWindow(nint windowHandle) => WindowNativeMethods.IsWindow(windowHandle);
    public bool Activate(nint windowHandle) => WindowNativeMethods.TryActivateWindow(windowHandle);

    public PasteTarget CaptureTarget()
    {
        RememberExternalWindow(ForegroundWindow);
        var handle = Interlocked.CompareExchange(ref _lastExternalWindow, 0, 0);
        if (!IsWindow(handle)) return new PasteTarget(0, null);
        WindowNativeMethods.GetWindowThreadProcessId(handle, out var processId);
        using var process = Process.GetProcessById((int)processId);
        return new PasteTarget(handle, process.ProcessName + ".exe");
    }

    private void RememberExternalWindow(nint handle)
    {
        if (handle == 0 || !IsWindow(handle)) return;
        WindowNativeMethods.GetWindowThreadProcessId(handle, out var processId);
        if (processId != 0 && processId != Environment.ProcessId)
        {
            Interlocked.Exchange(ref _lastExternalWindow, handle);
        }
    }

    private void OnForegroundChanged(nint hook, uint eventType, nint handle, int objectId, int childId, uint thread, uint time)
        => RememberExternalWindow(handle);

    public void Dispose()
    {
        if (!UnhookWinEvent(_hook)) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
    }

    private delegate void ForegroundEventCallback(nint hook, uint eventType, nint handle, int objectId, int childId, uint thread, uint time);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint SetWinEventHook(uint minimum, uint maximum, nint module, ForegroundEventCallback callback,
        uint processId, uint threadId, uint flags);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWinEvent(nint hook);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int key);
}
