using System.IO;
using System.Runtime.InteropServices;
using GestureClip.Core.Abstractions;
using GestureClip.Infrastructure.Win32;
using Microsoft.Extensions.Logging;

namespace GestureClip.Infrastructure.Clipboard;

public sealed class WpfClipboardWriter : IClipboardWriter, IDisposable
{
    private readonly ClipboardStaDispatcher _clipboardStaThread;
    private readonly ILogger<WpfClipboardWriter> _logger;

    public WpfClipboardWriter(ILogger<WpfClipboardWriter> logger)
    {
        _clipboardStaThread = new ClipboardStaDispatcher("GestureClip Clipboard Writer");
        _logger = logger;
    }

    public void Dispose()
    {
        _clipboardStaThread.Dispose();
    }

    public Task SetTextAsync(string text, CancellationToken cancellationToken)
    {
        return ClipboardRetryPolicy.RunAsync(
            () => _clipboardStaThread.InvokeAsync(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                SetUnicodeTextNative(text);
            }, cancellationToken),
            retryCount: 3,
            delay: TimeSpan.FromMilliseconds(20),
            cancellationToken);
    }

    public async Task SetImagePngBase64Async(string pngBase64, CancellationToken cancellationToken)
    {
        var imageData = await Task.Run(
            () =>
            {
                var pngBytes = ClipboardImageFactory.GetPngBytes(pngBase64);
                var image = ClipboardImageFactory.CreateFrozenBitmapImage(pngBytes);
                return new ClipboardImageData(
                    pngBytes,
                    image,
                    ClipboardImageFactory.CreateDibBytes(image));
            },
            cancellationToken);

        await ClipboardRetryPolicy.RunAsync(
            () => _clipboardStaThread.InvokeAsync(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                var dataObject = new System.Windows.DataObject();
                dataObject.SetImage(imageData.Image);
                dataObject.SetData("PNG", new MemoryStream(imageData.PngBytes, writable: false), autoConvert: false);
                dataObject.SetData(System.Windows.DataFormats.Dib, new MemoryStream(imageData.DibBytes, writable: false), autoConvert: false);
                System.Windows.Clipboard.SetDataObject(dataObject, copy: true);
            }, cancellationToken),
            retryCount: 3,
            delay: TimeSpan.FromMilliseconds(35),
            cancellationToken);
    }

    private static void SetUnicodeTextNative(string text)
    {
        var byteCount = checked((text.Length + 1) * 2);
        var handle = ClipboardNativeMethods.GlobalAlloc(ClipboardNativeMethods.GMEM_MOVEABLE, (UIntPtr)byteCount);
        if (handle == IntPtr.Zero)
        {
            throw new InvalidOperationException($"申请剪贴板内存失败。Win32={Marshal.GetLastWin32Error()}");
        }

        var ownershipTransferred = false;
        try
        {
            var target = ClipboardNativeMethods.GlobalLock(handle);
            if (target == IntPtr.Zero)
            {
                throw new InvalidOperationException($"锁定剪贴板内存失败。Win32={Marshal.GetLastWin32Error()}");
            }

            try
            {
                var bytes = System.Text.Encoding.Unicode.GetBytes(text + "\0");
                Marshal.Copy(bytes, 0, target, bytes.Length);
            }
            finally
            {
                ClipboardNativeMethods.GlobalUnlock(handle);
            }

            if (!ClipboardNativeMethods.OpenClipboard(IntPtr.Zero))
            {
                throw new InvalidOperationException($"打开系统剪贴板失败。Win32={Marshal.GetLastWin32Error()}");
            }

            try
            {
                if (!ClipboardNativeMethods.EmptyClipboard())
                {
                    throw new InvalidOperationException($"清空系统剪贴板失败。Win32={Marshal.GetLastWin32Error()}");
                }

                if (ClipboardNativeMethods.SetClipboardData(ClipboardNativeMethods.CF_UNICODETEXT, handle) == IntPtr.Zero)
                {
                    throw new InvalidOperationException($"写入系统剪贴板失败。Win32={Marshal.GetLastWin32Error()}");
                }

                ownershipTransferred = true;
            }
            finally
            {
                ClipboardNativeMethods.CloseClipboard();
            }
        }
        finally
        {
            if (!ownershipTransferred)
            {
                ClipboardNativeMethods.GlobalFree(handle);
            }
        }
    }

    public async Task SendPasteHotkeyAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var ok = await KeyboardPasteInjector.SendCtrlVAsync(
            _logger,
            cancellationToken,
            preferredTargetWindow: default,
            preferClipboardMessage: true);
        if (!ok)
        {
            // Do not throw: callers (history paste / assistant) already wrote clipboard;
            // throwing aborts UI flows without improving paste success rate.
            _logger.LogWarning("SendPasteHotkeyAsync: all paste injection paths failed.");
        }
    }

    private sealed record ClipboardImageData(
        byte[] PngBytes,
        System.Windows.Media.Imaging.BitmapSource Image,
        byte[] DibBytes);
}
