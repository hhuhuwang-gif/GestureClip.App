using System.Threading;

namespace GestureClip.App.Services;

public sealed class SingleInstanceService : IDisposable
{
    private const string MutexName = "Local\\GestureClip.MVP.SingleInstance";
    private const string ActivationEventName = "Local\\GestureClip.MVP.ActivationRequested";
    private Mutex? _mutex;
    private EventWaitHandle? _activationEvent;
    private CancellationTokenSource? _activationCts;
    private Task? _activationLoop;

    public event EventHandler? ActivationRequested;

    public bool TryAcquire()
    {
        _mutex = new Mutex(initiallyOwned: true, MutexName, out var createdNew);
        if (createdNew)
        {
            StartActivationListener();
        }

        return createdNew;
    }

    public void SignalExistingInstance()
    {
        try
        {
            using var activationEvent = EventWaitHandle.OpenExisting(ActivationEventName);
            activationEvent.Set();
        }
        catch (WaitHandleCannotBeOpenedException)
        {
        }
    }

    private void StartActivationListener()
    {
        _activationEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ActivationEventName);
        _activationCts = new CancellationTokenSource();
        var token = _activationCts.Token;
        _activationLoop = Task.Run(() =>
        {
            while (!token.IsCancellationRequested && _activationEvent is not null)
            {
                try
                {
                    if (_activationEvent.WaitOne(250))
                    {
                        ActivationRequested?.Invoke(this, EventArgs.Empty);
                    }
                }
                catch (ObjectDisposedException)
                {
                    return;
                }
            }
        }, token);
    }

    public void Dispose()
    {
        _activationCts?.Cancel();
        try
        {
            _activationEvent?.Set();
        }
        catch
        {
        }
        _activationEvent?.Dispose();
        _activationCts?.Dispose();
        _activationEvent = null;
        _activationCts = null;
        _activationLoop = null;

        if (_mutex is null)
        {
            return;
        }

        try
        {
            _mutex.ReleaseMutex();
        }
        catch (ApplicationException)
        {
        }

        _mutex.Dispose();
        _mutex = null;
    }
}
