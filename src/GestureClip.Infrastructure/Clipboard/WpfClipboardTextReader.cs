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
            return _dispatcher.Invoke(() =>
            {
                return TryReadRawPngBase64()
                    ?? TryReadDibPngBase64()
                    ?? TryReadWpfImagePngBase64()
                    ?? TryReadWinFormsImagePngBase64();
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read image from clipboard.");
            return null;
        }
    }

    private string? TryReadRawPngBase64()
    {
        try
        {
            var data = System.Windows.Clipboard.GetData("PNG");
            return ClipboardImageDataReader.TryGetPngBase64(data);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read PNG clipboard data.");
            return null;
        }
    }

    private string? TryReadDibPngBase64()
    {
        try
        {
            var data = System.Windows.Clipboard.GetData(System.Windows.DataFormats.Dib);
            return ClipboardImageDataReader.TryEncodeDibAsPngBase64(data);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read DIB clipboard data.");
            return null;
        }
    }

    private string? TryReadWpfImagePngBase64()
    {
        try
        {
            if (!System.Windows.Clipboard.ContainsImage())
            {
                return null;
            }

            var image = System.Windows.Clipboard.GetImage();
            return image is null ? null : EncodeBitmapSource(image);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read WPF clipboard image.");
            return null;
        }
    }

    private string? TryReadWinFormsImagePngBase64()
    {
        try
        {
            if (!System.Windows.Forms.Clipboard.ContainsImage())
            {
                return null;
            }

            using var image = System.Windows.Forms.Clipboard.GetImage();
            return image is null ? null : EncodeDrawingImage(image);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read WinForms clipboard image.");
            return null;
        }
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
        using var stream = new MemoryStream();
        image.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
        return Convert.ToBase64String(stream.ToArray());
    }
}
