using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;
using GestureClip.Core.Abstractions;
using GestureClip.Core.Gestures;
using GestureClip.Infrastructure.Win32;
using Microsoft.Extensions.Logging;

namespace GestureClip.Infrastructure.Gestures;

public sealed class LowLevelMouseHook : ILowLevelMouseHook, IDisposable
{
    private readonly Dispatcher _dispatcher;
    private readonly ILogger<LowLevelMouseHook> _logger;
    private readonly MouseHookNativeMethods.HookProc _hookProc;
    private IntPtr _hookHandle;
    private int _started;
    private long _receivedEventCount;

    public LowLevelMouseHook(ILogger<LowLevelMouseHook> logger)
    {
        _dispatcher = Application.Current.Dispatcher;
        _logger = logger;
        _hookProc = HookCallback;
    }

    public event EventHandler<MouseHookEventArgs>? MouseEventReceived;

    public void Start()
    {
        if (Interlocked.Exchange(ref _started, 1) == 1)
        {
            return;
        }

        _dispatcher.Invoke(() =>
        {
            var moduleName = Process.GetCurrentProcess().MainModule?.ModuleName;
            var moduleHandle = MouseHookNativeMethods.GetModuleHandle(moduleName);
            _hookHandle = MouseHookNativeMethods.SetWindowsHookEx(
                MouseHookNativeMethods.WhMouseLl,
                _hookProc,
                moduleHandle,
                0);

            if (_hookHandle == IntPtr.Zero)
            {
                Interlocked.Exchange(ref _started, 0);
                var error = Marshal.GetLastWin32Error();
                _logger.LogError(
                    "Failed to install low-level mouse hook. ModuleName={ModuleName}, ModuleHandle={ModuleHandle}, Win32Error={Win32Error}",
                    moduleName,
                    moduleHandle,
                    error);
                throw new InvalidOperationException($"Failed to install low-level mouse hook. Win32Error={error}");
            }

            _logger.LogInformation(
                "Low-level mouse hook started. ModuleName={ModuleName}, ModuleHandle={ModuleHandle}",
                moduleName,
                moduleHandle);
        });
    }

    public void Stop()
    {
        if (Interlocked.Exchange(ref _started, 0) == 0)
        {
            return;
        }

        _dispatcher.Invoke(() =>
        {
            if (_hookHandle != IntPtr.Zero)
            {
                MouseHookNativeMethods.UnhookWindowsHookEx(_hookHandle);
                _hookHandle = IntPtr.Zero;
                _logger.LogInformation("Low-level mouse hook stopped.");
            }
        });
    }

    public void Dispose()
    {
        Stop();
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

        return MouseHookNativeMethods.CallNextHookEx(_hookHandle, nCode, wParam, lParam);
    }

    private void LogHookEvent(MouseHookEvent hookEvent)
    {
        if (hookEvent.Type == MouseHookEventType.Move)
        {
            return;
        }

        var count = Interlocked.Increment(ref _receivedEventCount);
        _logger.LogInformation(
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
        var type = message switch
        {
            MouseHookNativeMethods.WmRButtonDown => MouseHookEventType.RightButtonDown,
            MouseHookNativeMethods.WmMouseMove => MouseHookEventType.Move,
            MouseHookNativeMethods.WmRButtonUp => MouseHookEventType.RightButtonUp,
            _ => (MouseHookEventType?)null
        };

        if (type is null)
        {
            hookEvent = default!;
            return false;
        }

        var data = Marshal.PtrToStructure<MouseHookNativeMethods.MSLLHOOKSTRUCT>(lParam);
        var isInjected = (data.flags & MouseHookNativeMethods.LlmhfInjected) != 0;
        hookEvent = new MouseHookEvent(type.Value, data.pt.x, data.pt.y, DateTimeOffset.UtcNow, isInjected);
        return true;
    }
}
