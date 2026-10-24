using GestureClip.Infrastructure.Win32;

namespace GestureClip.Infrastructure.Gestures;

public sealed partial class LowLevelMouseHook
{
    private sealed class HookThreadState
    {
        private int _cancellationDisposed;
        private int _isInstalled;

        public CancellationTokenSource Cancellation { get; } = new();

        public TaskCompletionSource<Exception?> Startup { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Stopped { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Thread? Thread { get; set; }

        public IntPtr HookHandle { get; set; }

        public uint NativeThreadId { get; set; }

        public bool IsInstalled => Volatile.Read(ref _isInstalled) == 1;

        public void MarkInstalled()
        {
            Volatile.Write(ref _isInstalled, 1);
            Startup.TrySetResult(null);
        }

        public void MarkStopped()
        {
            Volatile.Write(ref _isInstalled, 0);
            Startup.TrySetResult(new InvalidOperationException("Hook thread stopped before startup completed."));
            Stopped.TrySetResult();
        }

        public void ReportStartupFailure(Exception exception) => Startup.TrySetResult(exception);

        public void RequestCancellation()
        {
            try
            {
                Cancellation.Cancel();
            }
            catch (ObjectDisposedException)
            {
            }
        }

        public bool RequestShutdown()
        {
            return NativeThreadId != 0 && MouseHookNativeMethods.PostThreadMessage(
                NativeThreadId,
                MouseHookNativeMethods.WmQuit,
                UIntPtr.Zero,
                IntPtr.Zero);
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
