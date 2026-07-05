using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using GestureClip.Core.Abstractions;
using Microsoft.Extensions.Logging;

namespace GestureClip.Infrastructure.Clipboard;

public sealed class WpfClipboardTextReader : IClipboardTextReader
{
    private readonly Dispatcher _dispatcher;
    private readonly ILogger<WpfClipboardTextReader> _logger;

    public WpfClipboardTextReader(ILogger<WpfClipboardTextReader> logger)
    {
        _dispatcher = System.Windows.Application.Current.Dispatcher;
        _logger = logger;
    }

    public string? TryReadText()
    {
        try
        {
            return _dispatcher.Invoke(() =>
            {
                if (!System.Windows.Clipboard.ContainsText())
                {
                    return null;
                }

                return System.Windows.Clipboard.GetText();
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read text from clipboard.");
            return null;
        }
    }

    public string? TryReadImagePngBase64()
    {
        try
        {
            var snapshot = _dispatcher.Invoke(CaptureImageSnapshot);
            return EncodeImageSnapshot(snapshot);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read image from clipboard.");
            return null;
        }
    }

    private ImageClipboardSnapshot CaptureImageSnapshot()
    {
        var rawPngBytes = TryReadRawPngBytes();
        if (rawPngBytes is not null)
        {
            return new ImageClipboardSnapshot(rawPngBytes, null, null, null);
        }

        var dibBytes = TryReadDibBytes();
        if (dibBytes is not null)
        {
            return new ImageClipboardSnapshot(null, dibBytes, null, null);
        }

        var wpfImage = TryReadWpfImage();
        if (wpfImage is not null)
        {
            return new ImageClipboardSnapshot(null, null, wpfImage, null);
        }

        return new ImageClipboardSnapshot(null, null, null, TryReadWinFormsImageClone());
    }

    private string? EncodeImageSnapshot(ImageClipboardSnapshot snapshot)
    {
        return ClipboardImageDataReader.TryGetPngBase64(snapshot.RawPngBytes)
            ?? ClipboardImageDataReader.TryEncodeDibAsPngBase64(snapshot.DibBytes)
            ?? (snapshot.WpfImage is null ? null : EncodeBitmapSource(snapshot.WpfImage))
            ?? (snapshot.WinFormsImage is null ? null : EncodeDrawingImage(snapshot.WinFormsImage));
    }

    private byte[]? TryReadRawPngBytes()
    {
        try
        {
            if (!System.Windows.Clipboard.ContainsData("PNG"))
            {
                return null;
            }

            var data = System.Windows.Clipboard.GetData("PNG");
            return TryCopyBytes(data);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read PNG clipboard data.");
            return null;
        }
    }

    private byte[]? TryReadDibBytes()
    {
        try
        {
            if (!System.Windows.Clipboard.ContainsData(System.Windows.DataFormats.Dib))
            {
                return null;
            }

            var data = System.Windows.Clipboard.GetData(System.Windows.DataFormats.Dib);
            return TryCopyBytes(data);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read DIB clipboard data.");
            return null;
        }
    }

    private BitmapSource? TryReadWpfImage()
    {
        try
        {
            if (!System.Windows.Clipboard.ContainsImage())
            {
                return null;
            }

            var image = System.Windows.Clipboard.GetImage();
            image?.Freeze();
            return image;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read WPF clipboard image.");
            return null;
        }
    }

    private System.Drawing.Image? TryReadWinFormsImageClone()
    {
        try
        {
            if (!System.Windows.Forms.Clipboard.ContainsImage())
            {
                return null;
            }

            using var image = System.Windows.Forms.Clipboard.GetImage();
            return image is null ? null : new System.Drawing.Bitmap(image);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read WinForms clipboard image.");
            return null;
        }
    }

    private static byte[]? TryCopyBytes(object? data)
    {
        try
        {
            return data switch
            {
                null => null,
                byte[] bytes => bytes.ToArray(),
                MemoryStream memoryStream => memoryStream.ToArray(),
                Stream stream => CopyStream(stream),
                _ => null
            };
        }
        catch
        {
            return null;
        }
    }

    private static byte[] CopyStream(Stream stream)
    {
        if (stream.CanSeek)
        {
            stream.Position = 0;
        }

        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    }

    private static string EncodeBitmapSource(BitmapSource image)
    {
        image = RenderOnWhiteBackground(image);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(image));
        using var stream = new MemoryStream();
        encoder.Save(stream);
        return Convert.ToBase64String(stream.ToArray());
    }

    private static BitmapSource RenderOnWhiteBackground(BitmapSource image)
    {
        var width = Math.Max(1, image.PixelWidth);
        var height = Math.Max(1, image.PixelHeight);
        var visual = new DrawingVisual();

        using (var context = visual.RenderOpen())
        {
            context.DrawRectangle(System.Windows.Media.Brushes.White, null, new Rect(0, 0, width, height));
            context.DrawImage(image, new Rect(0, 0, width, height));
        }

        var rendered = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        rendered.Render(visual);
        rendered.Freeze();
        return rendered;
    }

    private static string EncodeDrawingImage(System.Drawing.Image image)
    {
        using (image)
        {
            using var stream = new MemoryStream();
            image.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
            return Convert.ToBase64String(stream.ToArray());
        }
    }

    private sealed record ImageClipboardSnapshot(
        byte[]? RawPngBytes,
        byte[]? DibBytes,
        BitmapSource? WpfImage,
        System.Drawing.Image? WinFormsImage);
}
