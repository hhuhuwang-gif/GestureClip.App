using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace GestureClip.App.Services;

internal sealed class MacroWindowHotkeys : IDisposable
{
    private const int RecordId = 8101, PlayId = 8102;
    private const int HotkeyMessage = 0x0312;
    private const uint NoRepeat = 0x4000, RecordKey = 0x77, PlayKey = 0x78;
    private readonly nint _handle;
    private readonly HwndSource _source;
    private readonly MacroRecorderService _recorder;

    public MacroWindowHotkeys(nint handle, MacroRecorderService recorder)
    {
        _handle = handle;
        _recorder = recorder;
        _source = HwndSource.FromHwnd(handle) ?? throw new InvalidOperationException("宏录制窗口尚未初始化。");
        Register(RecordId, RecordKey);
        try { Register(PlayId, PlayKey); }
        catch { UnregisterHotKey(_handle, RecordId); throw; }
        _source.AddHook(WindowProc);
    }

    private void Register(int id, uint key)
    {
        if (!RegisterHotKey(_handle, id, NoRepeat, key))
            throw new Win32Exception(Marshal.GetLastWin32Error(), $"{(key == RecordKey ? "F8" : key == PlayKey ? "F9" : "Esc")} 热键注册失败，可能已被其他程序占用。");
    }

    private nint WindowProc(nint hwnd, int message, nint parameter, nint data, ref bool handled)
    {
        if (message != HotkeyMessage) return 0;
        switch ((int)parameter)
        {
            case RecordId when _recorder.CanRecord: _ = _recorder.ToggleRecordingAsync(); break;
            case PlayId when _recorder.CanPlay: _ = _recorder.PlayAsync(); break;
        }
        handled = true;
        return 0;
    }

    public void Dispose()
    {
        _source.RemoveHook(WindowProc);
        UnregisterHotKey(_handle, RecordId);
        UnregisterHotKey(_handle, PlayId);
    }

    [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterHotKey(nint window, int id, uint modifiers, uint key);
    [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterHotKey(nint window, int id);
}
