using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Threading;
using GestureClip.Core.Abstractions;
using GestureClip.Core.Gestures;
using GestureClip.Infrastructure.Win32;
using Microsoft.Extensions.Logging;

namespace GestureClip.Infrastructure.Gestures;

public sealed class LowLevelMouseHook : ILowLevelMouseHook, IDisposable
{
    private static readonly TimeSpan HookThreadShutdownTimeout = TimeSpan.FromSeconds(2);

    private readonly ILogger<LowLevelMouseHook> _logger;
    private readonly MouseHookNativeMethods.HookProc _hookProc;
    private readonly object _startLock = new();
    private HookThreadState? _hookState;
    private int _startReferenceCount;
    private long _receivedEventCount;

    public LowLevelMouseHook(ILogger<LowLevelMouseHook> logger)
    {
        _logger = logger;
        _hookProc = HookCallback;
    }

    public event EventHandler<MouseHookEventArgs>? MouseEventReceived;

    public void Start()
    {
        lock (_startLock)
        {
            _startReferenceCount++;
            if (_startReferenceCount > 1)
            {
                return;
            }

            var state = new HookThreadState();
            state.Thread = new Thread(() => RunHookThread(state))
            {
                IsBackground = true,
                Name = "GestureClip.MouseHook"
            };
            state.Thread.SetApartmentState(ApartmentState.STA);
            _hookState = state;
            state.Thread.Start();
        }
    }

    public void Stop()
    {
        HookThreadState? state;
        lock (_startLock)
        {
            if (_startReferenceCount <= 0)
            {
                return;
            }

            _startReferenceCount--;
            if (_startReferenceCount > 0)
            {
                return;
            }

            state = _hookState;
            _hookState = null;
        }

        if (state is null)
        {
            return;
        }

        state.RequestCancellation();
        state.Dispatcher?.BeginInvokeShutdown(DispatcherPriority.Send);
        QueueShutdownObservation(state);
    }

    public void Dispose()
    {
        Stop();
    }

    private void RunHookThread(HookThreadState state)
    {
        state.Dispatcher = Dispatcher.CurrentDispatcher;
        try
        {
            if (state.Cancellation.IsCancellationRequested)
            {
                return;
            }

            var moduleName = Process.GetCurrentProcess().MainModule?.ModuleName;
            var moduleHandle = MouseHookNativeMethods.GetModuleHandle(moduleName);
            state.HookHandle = MouseHookNativeMethods.SetWindowsHookEx(
                MouseHookNativeMethods.WhMouseLl,
                _hookProc,
                moduleHandle,
                0);

            if (state.HookHandle == IntPtr.Zero)
            {
                var error = Marshal.GetLastWin32Error();
                _logger.LogError(
                    "Failed to install low-level mouse hook. ModuleName={ModuleName}, ModuleHandle={ModuleHandle}, Win32Error={Win32Error}",
                    moduleName,
                    moduleHandle,
                    error);
                return;
            }

            _logger.LogInformation(
                "Low-level mouse hook started on dedicated thread. ModuleName={ModuleName}, ModuleHandle={ModuleHandle}, ThreadId={ThreadId}",
                moduleName,
                moduleHandle,
                Environment.CurrentManagedThreadId);

            if (!state.Cancellation.IsCancellationRequested)
            {
                Dispatcher.Run();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Low-level mouse hook thread failed.");
        }
        finally
        {
            UninstallHookOnHookThread(state);
            lock (_startLock)
            {
                if (ReferenceEquals(_hookState, state))
                {
                    _hookState = null;
                    _startReferenceCount = 0;
                }
            }

            state.DisposeCancellation();
        }
    }

    private void QueueShutdownObservation(HookThreadState state)
    {
        var thread = state.Thread;
        if (thread is null)
        {
            state.DisposeCancellation();
            return;
        }

        _ = Task.Run(() =>
        {
            if (!thread.Join(HookThreadShutdownTimeout))
            {
                _logger.LogWarning("Low-level mouse hook thread did not stop within timeout.");
                return;
            }

            state.DisposeCancellation();
        });
    }

    private void UninstallHookOnHookThread(HookThreadState state)
    {
        if (state.HookHandle == IntPtr.Zero)
        {
            return;
        }

        try
        {
            if (!MouseHookNativeMethods.UnhookWindowsHookEx(state.HookHandle))
            {
                _logger.LogWarning("UnhookWindowsHookEx failed. Win32Error={Win32Error}", Marshal.GetLastWin32Error());
            }
        }
        finally
        {
            state.HookHandle = IntPtr.Zero;
            _logger.LogInformation("Low-level mouse hook stopped.");
        }
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        try
        {
            if (nCode >= 0 && TryCreateEvent(wParam, lParam, out var hookEvent))
            {
                LogHookEvent(hookEvent);
                var args = new MouseHookEventArgs { Event = hookEvent };
                MouseEventReceived?.Invoke(this, args);
                if (args.Suppress)
                {
                    return 1;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Low-level mouse hook callback failed.");
        }

        return MouseHookNativeMethods.CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
    }

    private void LogHookEvent(MouseHookEvent hookEvent)
    {
        if (hookEvent.Type == MouseHookEventType.Move)
        {
            return;
        }

        if (!_logger.IsEnabled(LogLevel.Debug))
        {
            return;
        }

        var count = Interlocked.Increment(ref _receivedEventCount);
        _logger.LogDebug(
            "Low-level mouse hook event received: {Type}, x={X}, y={Y}, injected={Injected}, count={Count}",
            hookEvent.Type,
            hookEvent.X,
            hookEvent.Y,
            hookEvent.IsInjected,
            count);
    }

    private static bool TryCreateEvent(IntPtr wParam, IntPtr lParam, out MouseHookEvent hookEvent)
    {
        var message = wParam.ToInt32();
        var data = Marshal.PtrToStructure<MouseHookNativeMethods.MSLLHOOKSTRUCT>(lParam);
        var xButton = data.mouseData >> 16;
        var type = message switch
        {
            MouseHookNativeMethods.WmLButtonDown => MouseHookEventType.LeftButtonDown,
            MouseHookNativeMethods.WmLButtonUp => MouseHookEventType.LeftButtonUp,
            MouseHookNativeMethods.WmRButtonDown => MouseHookEventType.RightButtonDown,
            MouseHookNativeMethods.WmMouseMove => MouseHookEventType.Move,
            MouseHookNativeMethods.WmRButtonUp => MouseHookEventType.RightButtonUp,
            MouseHookNativeMethods.WmMButtonDown => MouseHookEventType.MiddleButtonDown,
            MouseHookNativeMethods.WmMButtonUp => MouseHookEventType.MiddleButtonUp,
            MouseHookNativeMethods.WmMouseWheel => MouseHookEventType.Wheel,
            MouseHookNativeMethods.WmXButtonDown when xButton == MouseHookNativeMethods.XButton1 => MouseHookEventType.XButton1Down,
            MouseHookNativeMethods.WmXButtonUp when xButton == MouseHookNativeMethods.XButton1 => MouseHookEventType.XButton1Up,
            MouseHookNativeMethods.WmXButtonDown when xButton == MouseHookNativeMethods.XButton2 => MouseHookEventType.XButton2Down,
            MouseHookNativeMethods.WmXButtonUp when xButton == MouseHookNativeMethods.XButton2 => MouseHookEventType.XButton2Up,
            _ => (MouseHookEventType?)null
        };

        if (type is null)
        {
            hookEvent = default!;
            return false;
        }

        var isInjected = (data.flags & MouseHookNativeMethods.LlmhfInjected) != 0;
        var wheelDelta = type == MouseHookEventType.Wheel ? (short)xButton : 0;
        hookEvent = new MouseHookEvent(type.Value, data.pt.x, data.pt.y, DateTimeOffset.UtcNow, isInjected, wheelDelta);
        return true;
    }

    private sealed class HookThreadState
    {
        private int _cancellationDisposed;

        public CancellationTokenSource Cancellation { get; } = new();

        public Dispatcher? Dispatcher { get; set; }

        public Thread? Thread { get; set; }

        public IntPtr HookHandle { get; set; }

        public void RequestCancellation()
        {
            try
            {
                Cancellation.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // The hook thread may have failed and completed concurrently.
            }
        }

        public void DisposeCancellation()
        {
            if (Interlocked.Exchange(ref _cancellationDisposed, 1) == 0)
            {
                Cancellation.Dispose();
            }
        }
    }
}
