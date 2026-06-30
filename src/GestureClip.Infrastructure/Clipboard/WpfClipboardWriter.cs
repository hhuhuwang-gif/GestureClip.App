using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;
using GestureClip.Core.Abstractions;
using GestureClip.Infrastructure.Win32;
using Microsoft.Extensions.Logging;

namespace GestureClip.Infrastructure.Clipboard;

public sealed class WpfClipboardWriter : IClipboardWriter
{
    private const ushort VkControl = 0x11;
    private const ushort VkV = 0x56;
    private const uint InputKeyboard = 1;
    private const uint KeyEventKeyUp = 0x0002;
    private readonly Dispatcher _dispatcher;
    private readonly ILogger<WpfClipboardWriter> _logger;

    public WpfClipboardWriter(ILogger<WpfClipboardWriter> logger)
    {
        _dispatcher = Application.Current.Dispatcher;
        _logger = logger;
    }

    public Task SetTextAsync(string text, CancellationToken cancellationToken)
    {
        return _dispatcher.InvokeAsync(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            System.Windows.Clipboard.SetText(text);
        }).Task;
    }

    public Task SendPasteHotkeyAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var inputs = new[]
        {
            KeyDown(VkControl),
            KeyDown(VkV),
            KeyUp(VkV),
            KeyUp(VkControl)
        };

        var sent = ClipboardNativeMethods.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<ClipboardNativeMethods.INPUT>());
        if (sent != inputs.Length)
        {
            _logger.LogWarning("SendInput sent {SentInputCount} of {ExpectedInputCount} inputs.", sent, inputs.Length);
        }

        return Task.CompletedTask;
    }

    private static ClipboardNativeMethods.INPUT KeyDown(ushort virtualKey)
    {
        return KeyboardInput(virtualKey, 0);
    }

    private static ClipboardNativeMethods.INPUT KeyUp(ushort virtualKey)
    {
        return KeyboardInput(virtualKey, KeyEventKeyUp);
    }

    private static ClipboardNativeMethods.INPUT KeyboardInput(ushort virtualKey, uint flags)
    {
        return new ClipboardNativeMethods.INPUT
        {
            type = InputKeyboard,
            u = new ClipboardNativeMethods.InputUnion
            {
                ki = new ClipboardNativeMethods.KEYBDINPUT
                {
                    wVk = virtualKey,
                    dwFlags = flags
                }
            }
        };
    }
}
