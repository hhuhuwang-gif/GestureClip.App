using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using GestureClip.Core.Abstractions;
using GestureClip.Core.Gestures;
using GestureClip.Infrastructure.Win32;
using Microsoft.Extensions.Logging;

namespace GestureClip.Infrastructure.Gestures;

public sealed partial class LowLevelMouseHook : ILowLevelMouseHook, IDisposable
{
    private static readonly TimeSpan HookThreadStartupTimeout = TimeSpan.FromSeconds(2);
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

    public bool IsRunning
    {
        get
        {
            lock (_startLock)
            {
                return _hookState is { IsInstalled: true, Thread.IsAlive: true };
            }
        }
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var state = StartHookThread();
        if (state is null)
        {
            return;
        }

        try
        {
            await WaitForHookStartupAsync(state, cancellationToken);
        }
        catch
        {
            await StopAsync(CancellationToken.None);
            throw;
        }
    }

    private HookThreadState? StartHookThread()
    {
        lock (_startLock)
        {
            _startReferenceCount++;
            if (_startReferenceCount > 1)
            {
                return null;
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
            return state;
        }
    }

    private static async Task WaitForHookStartupAsync(
        HookThreadState state,
        CancellationToken cancellationToken)
    {
        var startupError = await state.Startup.Task.WaitAsync(HookThreadStartupTimeout, cancellationToken);
        if (startupError is not null)
        {
            throw new InvalidOperationException("Low-level mouse hook failed to start.", startupError);
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        var state = DetachHookState();
        if (state is null)
        {
            return;
        }

        state.RequestCancellation();
        if (!state.RequestShutdown() && state.Thread?.IsAlive == true)
        {
            _logger.LogWarning("Failed to post shutdown message to low-level mouse hook thread. Win32Error={Win32Error}", Marshal.GetLastWin32Error());
        }

        await state.Stopped.Task.WaitAsync(HookThreadShutdownTimeout, cancellationToken);
    }

    public void Dispose()
    {
        var state = DetachHookState();
        if (state is null)
        {
            return;
        }

        state.RequestCancellation();
        state.RequestShutdown();
    }

    private HookThreadState? DetachHookState()
    {
        lock (_startLock)
        {
            if (_startReferenceCount <= 0)
            {
                return null;
            }

            _startReferenceCount--;
            if (_startReferenceCount > 0)
            {
                return null;
            }

            var state = _hookState;
            _hookState = null;
            return state;
        }
    }

    private void RunHookThread(HookThreadState state)
    {
        state.NativeThreadId = MouseHookNativeMethods.GetCurrentThreadId();
        MouseHookNativeMethods.PeekMessage(out _, IntPtr.Zero, 0, 0, MouseHookNativeMethods.PmNoRemove);
        try
        {
            if (state.Cancellation.IsCancellationRequested)
            {
                return;
            }

            var moduleName = InstallHook(state);
            state.MarkInstalled();
            _logger.LogInformation(
                "Low-level mouse hook started on dedicated thread. ModuleName={ModuleName}, ThreadId={ThreadId}",
                moduleName,
                state.NativeThreadId);
            RunMessageLoop(state);
        }
        catch (Exception ex)
        {
            state.ReportStartupFailure(ex);
            _logger.LogError(ex, "Low-level mouse hook thread failed.");
        }
        finally
        {
            UninstallHookOnHookThread(state);
            state.MarkStopped();
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

    private string? InstallHook(HookThreadState state)
    {
        var moduleName = Process.GetCurrentProcess().MainModule?.ModuleName;
        var moduleHandle = MouseHookNativeMethods.GetModuleHandle(moduleName);
        state.HookHandle = MouseHookNativeMethods.SetWindowsHookEx(
            MouseHookNativeMethods.WhMouseLl,
            _hookProc,
            moduleHandle,
            0);
        if (state.HookHandle == IntPtr.Zero)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to install low-level mouse hook.");
        }

        return moduleName;
    }

    private static void RunMessageLoop(HookThreadState state)
    {
        while (!state.Cancellation.IsCancellationRequested)
        {
            var result = MouseHookNativeMethods.GetMessage(out var message, IntPtr.Zero, 0, 0);
            if (result == 0)
            {
                return;
            }

            if (result < 0)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Low-level mouse hook message loop failed.");
            }

            MouseHookNativeMethods.TranslateMessage(ref message);
            MouseHookNativeMethods.DispatchMessage(ref message);
        }
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

}
