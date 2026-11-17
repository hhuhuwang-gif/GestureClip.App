using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Threading;
using GestureClip.Core.Abstractions;
using GestureClip.Core.Macros;

namespace GestureClip.Infrastructure.Macros;

public sealed class WindowsMacroCapture : IMacroCapture
{
    private readonly List<MacroEvent> _events = [];
    private readonly MacroNative.HookProc _keyboardCallback;
    private readonly MacroNative.HookProc _mouseCallback;
    private Dispatcher? _dispatcher;
    private nint _keyboardHook, _mouseHook;
    private long _startedAt;
    private DateTimeOffset _startedAtUtc;
    private nint _excludedWindow;
    private Action? _playbackStop;
    private TaskCompletionSource _finished = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public WindowsMacroCapture()
    {
        _keyboardCallback = OnKeyboard;
        _mouseCallback = OnMouse;
    }

    public Task StartAsync(nint excludedWindow) => StartCoreAsync(excludedWindow, null);
    public Task StartPlaybackStopAsync(Action stop) => StartCoreAsync(0, stop);

    public async Task ExcludeGestureAsync(MacroGestureInterval gesture)
    {
        var dispatcher = _dispatcher ?? throw new InvalidOperationException("当前没有录制。");
        await dispatcher.InvokeAsync(() =>
        {
            var retained = MacroControlGestureFilter.Exclude(_events, _startedAtUtc, gesture);
            _events.Clear();
            _events.AddRange(retained);
        }).Task.ConfigureAwait(false);
    }

    private async Task StartCoreAsync(nint excludedWindow, Action? stop)
    {
        if (_dispatcher is not null) throw new InvalidOperationException("正在录制。");
        _excludedWindow = excludedWindow;
        _playbackStop = stop;
        var ready = new TaskCompletionSource<Dispatcher>(TaskCreationOptions.RunContinuationsAsynchronously);
        _finished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() => RunCaptureThread(ready)) { IsBackground = true, Name = "GestureClip Macro Capture" };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        _dispatcher = await ready.Task;
    }

    private void RunCaptureThread(TaskCompletionSource<Dispatcher> ready)
    {
        try
        {
            var dispatcher = Dispatcher.CurrentDispatcher;
            _events.Clear();
            var module = MacroNative.GetModuleHandle(null);
            _keyboardHook = MacroNative.SetWindowsHookEx(MacroNative.KeyboardHook, _keyboardCallback, module, 0);
            if (_keyboardHook == 0) throw new Win32Exception(Marshal.GetLastWin32Error(), "安装键盘录制钩子失败。");
            if (_playbackStop is null)
            {
                _mouseHook = MacroNative.SetWindowsHookEx(MacroNative.MouseHook, _mouseCallback, module, 0);
                if (_mouseHook == 0) throw new Win32Exception(Marshal.GetLastWin32Error(), "安装鼠标录制钩子失败。");
            }
            _startedAt = Stopwatch.GetTimestamp();
            _startedAtUtc = DateTimeOffset.UtcNow;
            ready.SetResult(dispatcher);
            Dispatcher.Run();
        }
        catch (Exception error) { ready.TrySetException(error); _finished.TrySetException(error); }
        finally
        {
            var errors = RemoveHooks();
            if (errors.Count > 0) _finished.TrySetException(new AggregateException(errors));
            else _finished.TrySetResult();
        }
    }

    public async Task<IReadOnlyList<MacroEvent>> StopAsync()
    {
        var dispatcher = _dispatcher ?? throw new InvalidOperationException("当前没有录制。");
        await dispatcher.InvokeAsync(() => dispatcher.BeginInvokeShutdown(DispatcherPriority.Send)).Task.ConfigureAwait(false);
        await _finished.Task.ConfigureAwait(false);
        _dispatcher = null;
        var result = _events.ToArray();
        _events.Clear();
        return result;
    }

    public async ValueTask DisposeAsync()
    {
        if (_dispatcher is not null) await StopAsync().ConfigureAwait(false);
    }
    public void Dispose() => DisposeAsync().AsTask().GetAwaiter().GetResult();

    private List<Exception> RemoveHooks()
    {
        List<Exception> errors = [];
        foreach (var handle in new[] { _keyboardHook, _mouseHook })
            if (handle != 0 && !MacroNative.UnhookWindowsHookEx(handle)) errors.Add(new Win32Exception(Marshal.GetLastWin32Error()));
        _keyboardHook = _mouseHook = 0;
        return errors;
    }

    private nint OnKeyboard(int code, nint message, nint pointer)
    {
        if (code < 0) return MacroNative.CallNextHookEx(0, code, message, pointer);
        var data = Marshal.PtrToStructure<MacroNative.KeyboardData>(pointer);
        var kind = (uint)message;
        if (_playbackStop is { } stop)
        {
            const uint escape = 0x1B;
            if (data.Key == escape && (data.Flags & MacroNative.KeyboardInjected) == 0
                && kind is MacroNative.KeyDown or MacroNative.SysKeyDown) stop();
            return MacroNative.CallNextHookEx(0, code, message, pointer);
        }
        if ((data.Flags & MacroNative.KeyboardInjected) == 0 && data.Key is not (MacroNative.F8 or MacroNative.F9)
            && MacroNative.GetForegroundWindow() != _excludedWindow)
        {
            if (kind is MacroNative.KeyDown or MacroNative.SysKeyDown or MacroNative.KeyUp or MacroNative.SysKeyUp)
                _events.Add(new MacroEvent
                {
                    OffsetTicks = ElapsedTicks(), Code = data.Key, ScanCode = data.Scan, Extended = (data.Flags & 1) != 0,
                    Kind = kind is MacroNative.KeyDown or MacroNative.SysKeyDown ? MacroEventKind.KeyDown : MacroEventKind.KeyUp
                });
        }
        return MacroNative.CallNextHookEx(0, code, message, pointer);
    }

    private nint OnMouse(int code, nint message, nint pointer)
    {
        if (code < 0) return MacroNative.CallNextHookEx(0, code, message, pointer);
        var data = Marshal.PtrToStructure<MacroNative.MouseData>(pointer);
        var kind = MouseKind((uint)message);
        var target = MacroNative.WindowFromPoint(new MacroNative.NativePoint { X = data.X, Y = data.Y });
        const uint rootWindow = 2;
        if ((data.Flags & MacroNative.MouseInjected) == 0 && kind is { } value
            && MacroNative.GetAncestor(target, rootWindow) != _excludedWindow)
            _events.Add(new MacroEvent
            {
                OffsetTicks = ElapsedTicks(), Kind = value, X = data.X, Y = data.Y,
                Code = value is MacroEventKind.XDown or MacroEventKind.XUp ? data.Data >> 16 :
                    value is MacroEventKind.Wheel or MacroEventKind.HorizontalWheel ? unchecked((uint)(int)(short)(data.Data >> 16)) : 0
            });
        return MacroNative.CallNextHookEx(0, code, message, pointer);
    }

    private long ElapsedTicks() => Stopwatch.GetElapsedTime(_startedAt).Ticks;

    private static MacroEventKind? MouseKind(uint message) => message switch
    {
        MacroNative.Move => MacroEventKind.Move, MacroNative.LeftDown => MacroEventKind.LeftDown,
        MacroNative.LeftUp => MacroEventKind.LeftUp, MacroNative.RightDown => MacroEventKind.RightDown,
        MacroNative.RightUp => MacroEventKind.RightUp, MacroNative.MiddleDown => MacroEventKind.MiddleDown,
        MacroNative.MiddleUp => MacroEventKind.MiddleUp, MacroNative.Wheel => MacroEventKind.Wheel,
        MacroNative.HWheel => MacroEventKind.HorizontalWheel, MacroNative.XDown => MacroEventKind.XDown,
        MacroNative.XUp => MacroEventKind.XUp, _ => null
    };
}
