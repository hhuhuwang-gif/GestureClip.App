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
    private const int OpenQuickActionHotkeyId = 0x4744;
    private const int PastePlainTextHotkeyId = 0x4745;
    private const int PasteQueueHotkeyId = 0x4746;
    private readonly Dispatcher _dispatcher;
    private HwndSource? _source;
    private int _lastError;

    public WindowsHotkeyRegistrar()
    {
        _dispatcher = System.Windows.Application.Current.Dispatcher;
    }

    public event EventHandler? HotkeyPressed;

    public event EventHandler? QuickActionHotkeyPressed;

    public event EventHandler? PastePlainTextHotkeyPressed;

    public event EventHandler? PasteQueueHotkeyPressed;

    public bool RegisterOpenClipboardHotkey(HotkeyDefinition hotkey)
    {
        return Register(OpenClipboardOverlayHotkeyId, hotkey);
    }

    public void UnregisterOpenClipboardHotkey()
    {
        Unregister(OpenClipboardOverlayHotkeyId);
    }

    public bool RegisterOpenQuickActionHotkey(HotkeyDefinition hotkey)
    {
        return Register(OpenQuickActionHotkeyId, hotkey);
    }

    public void UnregisterOpenQuickActionHotkey()
    {
        Unregister(OpenQuickActionHotkeyId);
    }

    public bool RegisterPastePlainTextHotkey(HotkeyDefinition hotkey)
    {
        return Register(PastePlainTextHotkeyId, hotkey);
    }

    public void UnregisterPastePlainTextHotkey()
    {
        Unregister(PastePlainTextHotkeyId);
    }

    public bool RegisterPasteQueueHotkey(HotkeyDefinition hotkey)
    {
        return Register(PasteQueueHotkeyId, hotkey);
    }

    public void UnregisterPasteQueueHotkey()
    {
        Unregister(PasteQueueHotkeyId);
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
            HotkeyNativeMethods.UnregisterHotKey(_source.Handle, OpenQuickActionHotkeyId);
            HotkeyNativeMethods.UnregisterHotKey(_source.Handle, PastePlainTextHotkeyId);
            HotkeyNativeMethods.UnregisterHotKey(_source.Handle, PasteQueueHotkeyId);
            _source.RemoveHook(WndProc);
            _source.Dispose();
            _source = null;
        });
    }

    private bool Register(int hotkeyId, HotkeyDefinition hotkey)
    {
        return _dispatcher.Invoke(() =>
        {
            EnsureSource();
            HotkeyNativeMethods.UnregisterHotKey(_source!.Handle, hotkeyId);
            var ok = HotkeyNativeMethods.RegisterHotKey(
                _source.Handle,
                hotkeyId,
                hotkey.Modifiers,
                hotkey.VirtualKey);
            _lastError = ok ? 0 : Marshal.GetLastWin32Error();
            return ok;
        });
    }

    private void Unregister(int hotkeyId)
    {
        _dispatcher.Invoke(() =>
        {
            if (_source is null)
            {
                return;
            }

            HotkeyNativeMethods.UnregisterHotKey(_source.Handle, hotkeyId);
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
        if (msg != HotkeyNativeMethods.WmHotkey)
        {
            return IntPtr.Zero;
        }

        var id = wParam.ToInt32();
        if (id == OpenClipboardOverlayHotkeyId)
        {
            HotkeyPressed?.Invoke(this, EventArgs.Empty);
            handled = true;
        }
        else if (id == OpenQuickActionHotkeyId)
        {
            QuickActionHotkeyPressed?.Invoke(this, EventArgs.Empty);
            handled = true;
        }
        else if (id == PastePlainTextHotkeyId)
        {
            PastePlainTextHotkeyPressed?.Invoke(this, EventArgs.Empty);
            handled = true;
        }
        else if (id == PasteQueueHotkeyId)
        {
            PasteQueueHotkeyPressed?.Invoke(this, EventArgs.Empty);
            handled = true;
        }

        return IntPtr.Zero;
    }
}
