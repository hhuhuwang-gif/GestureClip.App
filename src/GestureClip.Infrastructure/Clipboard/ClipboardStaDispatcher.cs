using System.Windows.Threading;

namespace GestureClip.Infrastructure.Clipboard;

internal sealed class ClipboardStaDispatcher : IDisposable
{
    private readonly TaskCompletionSource<Dispatcher> _dispatcherSource =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Thread _thread;
    private Dispatcher? _dispatcher;
    private bool _disposed;

    public ClipboardStaDispatcher(string name)
    {
        _thread = new Thread(ThreadMain)
        {
            IsBackground = true,
            Name = name
        };
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();
    }

    public T Invoke<T>(Func<T> action)
    {
        ThrowIfDisposed();
        var dispatcher = _dispatcherSource.Task.GetAwaiter().GetResult();
        return dispatcher.Invoke(action, DispatcherPriority.Send);
    }

    public void Invoke(Action action)
    {
        ThrowIfDisposed();
        var dispatcher = _dispatcherSource.Task.GetAwaiter().GetResult();
        dispatcher.Invoke(action, DispatcherPriority.Send);
    }

    public Task<T> InvokeAsync<T>(Func<T> action, CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        var dispatcher = _dispatcherSource.Task.GetAwaiter().GetResult();
        return dispatcher.InvokeAsync(action, DispatcherPriority.Send, cancellationToken).Task;
    }

    public Task InvokeAsync(Action action, CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        var dispatcher = _dispatcherSource.Task.GetAwaiter().GetResult();
        return dispatcher.InvokeAsync(action, DispatcherPriority.Send, cancellationToken).Task;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        var dispatcher = _dispatcher;
        if (dispatcher is not null && !dispatcher.HasShutdownStarted)
        {
            dispatcher.BeginInvokeShutdown(DispatcherPriority.Send);
        }
    }

    private void ThreadMain()
    {
        _dispatcher = Dispatcher.CurrentDispatcher;
        _dispatcherSource.SetResult(_dispatcher);
        Dispatcher.Run();
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(ClipboardStaDispatcher));
        }
    }
}
