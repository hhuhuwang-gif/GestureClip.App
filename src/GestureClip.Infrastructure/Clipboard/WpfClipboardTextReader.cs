using System.Collections.Specialized;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using GestureClip.Core.Abstractions;
using GestureClip.Core.Clipboard;
using Microsoft.Extensions.Logging;

namespace GestureClip.Infrastructure.Clipboard;

public sealed class WpfClipboardTextReader : IClipboardTextReader, IDisposable
{
    private const int DibV5ClipboardFormatId = 17;
    private const long MaxClipboardImageFileBytes = 50L * 1024 * 1024;
    private static readonly string[] ExtraTextFormats =
    [
        System.Windows.DataFormats.StringFormat,
        System.Windows.DataFormats.Text,
        System.Windows.DataFormats.OemText,
        System.Windows.DataFormats.CommaSeparatedValue,
        "UTF8_STRING",
        "text/plain",
        "text/plain;charset=utf-8",
        "System.String"
    ];
    private static readonly string[] EncodedImageFormats =
    [
        "JFIF",
        "image/jpeg",
        "image/jpg",
        System.Windows.DataFormats.Tiff,
        "GIF",
        "image/gif",
        "image/bmp"
    ];
    private static readonly HashSet<string> SupportedImageFileExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".tif", ".tiff"
    };
    private static readonly Regex HtmlDataImageRegex = new(
        @"data:image/(?:png|jpe?g|gif|bmp|tiff);base64,(?<data>[A-Za-z0-9+/=\s]+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly ClipboardStaDispatcher _clipboardStaThread;
    private readonly ILogger<WpfClipboardTextReader> _logger;

    public WpfClipboardTextReader(ILogger<WpfClipboardTextReader> logger)
    {
        _clipboardStaThread = new ClipboardStaDispatcher("GestureClip Clipboard Reader");
        _logger = logger;
    }

    public void Dispose()
    {
        _clipboardStaThread.Dispose();
    }

    public ClipboardReadSnapshot TryReadSnapshot()
    {
        try
        {
            var captured = _clipboardStaThread.Invoke(CaptureContentSnapshot);
            var image = captured.Image is null ? null : EncodeImageSnapshot(captured.Image);
            if (image is null && captured.Image is not null)
            {
                var fallback = _clipboardStaThread.Invoke(CaptureFallbackImageSnapshot);
                image = fallback is null ? null : EncodeImageSnapshot(fallback);
            }

            return new ClipboardReadSnapshot(
                captured.Text,
                image,
                captured.PreferImage && image is not null);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read clipboard snapshot.");
            return new ClipboardReadSnapshot(null, null);
        }
    }

    public string? TryReadText()
    {
        try
        {
            return _clipboardStaThread.Invoke(ReadBestEffortText);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read text from clipboard.");
            return null;
        }
    }

    /// <summary>
    /// Prefer Unicode text, then auto-converted text, common native/custom text formats,
    /// RTF and finally HTML-to-plain. Must stay on the clipboard STA thread.
    /// </summary>
    private string? ReadBestEffortText()
    {
        try
        {
            if (System.Windows.Clipboard.ContainsText(System.Windows.TextDataFormat.UnicodeText))
            {
                var unicode = System.Windows.Clipboard.GetText(System.Windows.TextDataFormat.UnicodeText);
                if (!string.IsNullOrEmpty(unicode))
                {
                    return unicode;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "UnicodeText clipboard read failed; trying fallbacks.");
        }

        try
        {
            if (System.Windows.Clipboard.ContainsText())
            {
                var text = System.Windows.Clipboard.GetText();
                if (!string.IsNullOrEmpty(text))
                {
                    return text;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Generic text clipboard read failed; trying native formats.");
        }

        foreach (var format in ExtraTextFormats)
        {
            var text = TryReadTextFormat(format);
            if (!string.IsNullOrEmpty(text))
            {
                return text;
            }
        }

        try
        {
            if (System.Windows.Clipboard.ContainsData(System.Windows.DataFormats.Rtf))
            {
                var rtf = ClipboardTextDataReader.TryGetText(
                    System.Windows.Clipboard.GetData(System.Windows.DataFormats.Rtf),
                    System.Windows.DataFormats.Rtf);
                var plain = ClipboardRtfText.ExtractPlain(rtf);
                if (!string.IsNullOrEmpty(plain))
                {
                    return plain;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "RTF clipboard read failed; trying HTML.");
        }

        try
        {
            if (System.Windows.Clipboard.ContainsText(System.Windows.TextDataFormat.Html))
            {
                var html = System.Windows.Clipboard.GetText(System.Windows.TextDataFormat.Html);
                var plain = ClipboardHtmlText.ExtractPlain(html);
                if (!string.IsNullOrEmpty(plain))
                {
                    return plain;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "HTML clipboard read failed.");
        }

        return null;
    }

    private string? TryReadTextFormat(string format)
    {
        try
        {
            if (!System.Windows.Clipboard.ContainsData(format))
            {
                return null;
            }

            return ClipboardTextDataReader.TryGetText(System.Windows.Clipboard.GetData(format), format);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Clipboard text format {Format} read failed.", format);
            return null;
        }
    }

    public string? TryReadImagePngBase64()
    {
        try
        {
            var snapshot = _clipboardStaThread.Invoke(CaptureImageSnapshot);
            var image = EncodeImageSnapshot(snapshot);
            if (image is not null)
            {
                return image;
            }

            var fallback = _clipboardStaThread.Invoke(CaptureFallbackImageSnapshot);
            return fallback is null ? null : EncodeImageSnapshot(fallback);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read image from clipboard.");
            return null;
        }
    }

    private CapturedClipboardSnapshot CaptureContentSnapshot()
    {
        var text = ReadBestEffortText();
        var explicitImage = CaptureExplicitImageSnapshot();
        if (explicitImage is not null)
        {
            return new CapturedClipboardSnapshot(text, explicitImage, PreferImage: true);
        }

        if (!string.IsNullOrWhiteSpace(text))
        {
            return new CapturedClipboardSnapshot(text, null, PreferImage: false);
        }

        var fallbackImage = CaptureFallbackImageSnapshot();
        return new CapturedClipboardSnapshot(null, fallbackImage, PreferImage: fallbackImage is not null);
    }

    private ImageClipboardSnapshot CaptureImageSnapshot()
    {
        return CaptureExplicitImageSnapshot()
            ?? CaptureFallbackImageSnapshot()
            ?? ImageClipboardSnapshot.Empty;
    }

    private ImageClipboardSnapshot? CaptureExplicitImageSnapshot()
    {
        var rawPngBytes = TryReadRawPngBytes();
        if (rawPngBytes is not null)
        {
            return new ImageClipboardSnapshot(rawPngBytes, null, null, null);
        }

        var encodedImageBytes = TryReadEncodedImageBytes();
        if (encodedImageBytes is not null)
        {
            return new ImageClipboardSnapshot(encodedImageBytes, null, null, null);
        }

        var dibV5Bytes = TryReadDibV5Bytes();
        if (dibV5Bytes is not null)
        {
            return new ImageClipboardSnapshot(null, dibV5Bytes, null, null);
        }

        var fileImageBytes = TryReadFileDropImageBytes();
        if (fileImageBytes is not null)
        {
            return new ImageClipboardSnapshot(fileImageBytes, null, null, null);
        }

        var htmlImageBytes = TryReadHtmlDataImageBytes();
        return htmlImageBytes is null
            ? null
            : new ImageClipboardSnapshot(htmlImageBytes, null, null, null);
    }

    private ImageClipboardSnapshot? CaptureFallbackImageSnapshot()
    {
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

        var winFormsImage = TryReadWinFormsImageClone();
        return winFormsImage is null
            ? null
            : new ImageClipboardSnapshot(null, null, null, winFormsImage);
    }

    private string? EncodeImageSnapshot(ImageClipboardSnapshot snapshot)
    {
        return ClipboardImageDataReader.TryGetPngBase64(snapshot.RawImageBytes)
            ?? ClipboardImageDataReader.TryEncodeImageAsPngBase64(snapshot.RawImageBytes)
            ?? ClipboardImageDataReader.TryEncodeDibAsPngBase64(snapshot.DibBytes)
            ?? (snapshot.WpfImage is null ? null : EncodeBitmapSource(snapshot.WpfImage))
            ?? (snapshot.WinFormsImage is null ? null : EncodeDrawingImage(snapshot.WinFormsImage));
    }

    private byte[]? TryReadRawPngBytes()
    {
        foreach (var format in new[] { "PNG", "image/png" })
        {
            try
            {
                if (format == "PNG" && !System.Windows.Clipboard.ContainsData("PNG"))
                {
                    continue;
                }

                if (format != "PNG" && !System.Windows.Clipboard.ContainsData(format))
                {
                    continue;
                }

                var bytes = TryCopyBytes(System.Windows.Clipboard.GetData(format));
                if (bytes is { Length: > 0 })
                {
                    return bytes;
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to read PNG clipboard format {Format}.", format);
            }
        }

        return null;
    }

    private byte[]? TryReadEncodedImageBytes()
    {
        foreach (var format in EncodedImageFormats)
        {
            try
            {
                if (!System.Windows.Clipboard.ContainsData(format))
                {
                    continue;
                }

                var bytes = TryCopyBytes(System.Windows.Clipboard.GetData(format));
                if (bytes is { Length: > 0 })
                {
                    return bytes;
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to read encoded clipboard image format {Format}.", format);
            }
        }

        return null;
    }

    private byte[]? TryReadDibV5Bytes()
    {
        try
        {
            var format = System.Windows.DataFormats.GetDataFormat(DibV5ClipboardFormatId).Name;
            if (!System.Windows.Clipboard.ContainsData(format))
            {
                return null;
            }

            return TryCopyBytes(System.Windows.Clipboard.GetData(format));
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to read DIBV5 clipboard data.");
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

    private byte[]? TryReadFileDropImageBytes()
    {
        try
        {
            if (!System.Windows.Clipboard.ContainsFileDropList())
            {
                return null;
            }

            StringCollection files = System.Windows.Clipboard.GetFileDropList();
            foreach (var path in files.Cast<string>())
            {
                var extension = Path.GetExtension(path);
                if (!SupportedImageFileExtensions.Contains(extension) || !File.Exists(path))
                {
                    continue;
                }

                var info = new FileInfo(path);
                if (info.Length is <= 0 or > MaxClipboardImageFileBytes)
                {
                    continue;
                }

                return File.ReadAllBytes(path);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to read image file from clipboard file-drop data.");
        }

        return null;
    }

    private byte[]? TryReadHtmlDataImageBytes()
    {
        try
        {
            if (!System.Windows.Clipboard.ContainsText(System.Windows.TextDataFormat.Html))
            {
                return null;
            }

            var html = System.Windows.Clipboard.GetText(System.Windows.TextDataFormat.Html);
            var match = HtmlDataImageRegex.Match(html);
            if (!match.Success)
            {
                return null;
            }

            var compact = new string(match.Groups["data"].Value.Where(character => !char.IsWhiteSpace(character)).ToArray());
            return Convert.FromBase64String(compact);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to read data-URI image from clipboard HTML.");
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

    private sealed record CapturedClipboardSnapshot(
        string? Text,
        ImageClipboardSnapshot? Image,
        bool PreferImage);

    private sealed record ImageClipboardSnapshot(
        byte[]? RawImageBytes,
        byte[]? DibBytes,
        BitmapSource? WpfImage,
        System.Drawing.Image? WinFormsImage)
    {
        public static ImageClipboardSnapshot Empty { get; } = new(null, null, null, null);
    }
}
