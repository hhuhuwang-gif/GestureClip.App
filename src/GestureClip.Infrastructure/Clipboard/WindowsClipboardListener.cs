using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using GestureClip.Core.Abstractions;
using GestureClip.Core.Clipboard;
using GestureClip.Infrastructure.Win32;
using Microsoft.Extensions.Logging;

namespace GestureClip.Infrastructure.Clipboard;

public sealed class WindowsClipboardListener : IClipboardListener, IDisposable
{
    private readonly Dispatcher _dispatcher;
    private readonly ILogger<WindowsClipboardListener> _logger;
    private HwndSource? _source;
    private int _started;

    public WindowsClipboardListener(ILogger<WindowsClipboardListener> logger)
    {
        _dispatcher = Application.Current.Dispatcher;
        _logger = logger;
    }

    public event EventHandler<ClipboardChangedEventArgs>? ClipboardChanged;

    public void Start()
    {
        if (Interlocked.Exchange(ref _started, 1) == 1)
        {
            return;
        }

        _dispatcher.Invoke(() =>
        {
            var parameters = new HwndSourceParameters("GestureClipClipboardListener")
            {
                Width = 0,
                Height = 0,
                WindowStyle = 0
            };

            _source = new HwndSource(parameters);
            _source.AddHook(WndProc);

            if (!ClipboardNativeMethods.AddClipboardFormatListener(_source.Handle))
            {
                _source.RemoveHook(WndProc);
                _source.Dispose();
                _source = null;
                Interlocked.Exchange(ref _started, 0);
                throw new InvalidOperationException("AddClipboardFormatListener failed.");
            }

            _logger.LogInformation("Windows clipboard listener started.");
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
            if (_source is null)
            {
                return;
            }

            ClipboardNativeMethods.RemoveClipboardFormatListener(_source.Handle);
            _source.RemoveHook(WndProc);
            _source.Dispose();
            _source = null;
            _logger.LogInformation("Windows clipboard listener stopped.");
        });
    }

    public void Dispose()
    {
        Stop();
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == ClipboardNativeMethods.WmClipboardUpdate)
        {
            ClipboardChanged?.Invoke(this, new ClipboardChangedEventArgs());
            handled = true;
        }

        return IntPtr.Zero;
    }
}
