using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using GestureClip.Core.Abstractions;
using GestureClip.Core.Hotkeys;
using GestureClip.Infrastructure.Win32;

namespace GestureClip.Infrastructure.Hotkeys;

public sealed class WindowsHotkeyRegistrar : IHotkeyRegistrar, IDisposable
{
    private const int OpenClipboardOverlayHotkeyId = 0x4743;
    private readonly Dispatcher _dispatcher;
    private HwndSource? _source;
    private int _lastError;

    public WindowsHotkeyRegistrar()
    {
        _dispatcher = Application.Current.Dispatcher;
    }

    public event EventHandler? HotkeyPressed;

    public bool RegisterOpenClipboardHotkey(HotkeyDefinition hotkey)
    {
        return _dispatcher.Invoke(() =>
        {
            EnsureSource();
            var ok = HotkeyNativeMethods.RegisterHotKey(
                _source!.Handle,
                OpenClipboardOverlayHotkeyId,
                hotkey.Modifiers,
                hotkey.VirtualKey);
            _lastError = ok ? 0 : Marshal.GetLastWin32Error();
            return ok;
        });
    }

    public void UnregisterOpenClipboardHotkey()
    {
        _dispatcher.Invoke(() =>
        {
            if (_source is null)
            {
                return;
            }

            HotkeyNativeMethods.UnregisterHotKey(_source.Handle, OpenClipboardOverlayHotkeyId);
        });
    }

    public int GetLastError() => _lastError;

    public void Dispose()
    {
        _dispatcher.Invoke(() =>
        {
            if (_source is null)
            {
                return;
            }

            HotkeyNativeMethods.UnregisterHotKey(_source.Handle, OpenClipboardOverlayHotkeyId);
            _source.RemoveHook(WndProc);
            _source.Dispose();
            _source = null;
        });
    }

    private void EnsureSource()
    {
        if (_source is not null)
        {
            return;
        }

        var parameters = new HwndSourceParameters("GestureClipGlobalHotkey")
        {
            Width = 0,
            Height = 0,
            WindowStyle = 0
        };
        _source = new HwndSource(parameters);
        _source.AddHook(WndProc);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == HotkeyNativeMethods.WmHotkey && wParam.ToInt32() == OpenClipboardOverlayHotkeyId)
        {
            HotkeyPressed?.Invoke(this, EventArgs.Empty);
            handled = true;
        }

        return IntPtr.Zero;
    }
}
