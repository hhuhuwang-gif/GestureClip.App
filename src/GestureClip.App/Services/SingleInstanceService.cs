using System.Threading;

namespace GestureClip.App.Services;

public sealed class SingleInstanceService : IDisposable
{
    private const string MutexName = "Local\\GestureClip.MVP.SingleInstance";
    private Mutex? _mutex;

    public bool TryAcquire()
    {
        _mutex = new Mutex(initiallyOwned: true, MutexName, out var createdNew);
        return createdNew;
    }

    public void Dispose()
    {
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
