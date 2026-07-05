using System.IO;
using System.Windows.Media.Imaging;

namespace GestureClip.Infrastructure.Clipboard;

public static class ClipboardImageFactory
{
    private const int BitmapFileHeaderSize = 14;

    public static byte[] GetPngBytes(string pngBase64)
    {
        return Convert.FromBase64String(NormalizeBase64(pngBase64));
    }

    public static BitmapImage CreateFrozenBitmapImage(string pngBase64)
    {
        var bytes = GetPngBytes(pngBase64);
        return CreateFrozenBitmapImage(bytes);
    }

    public static BitmapImage CreateFrozenBitmapImage(byte[] pngBytes)
    {
        ArgumentNullException.ThrowIfNull(pngBytes);

        using var stream = new MemoryStream(pngBytes);
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
        image.StreamSource = stream;
        image.EndInit();
        image.Freeze();
        return image;
    }

    public static byte[] CreateDibBytes(string pngBase64)
    {
        var image = CreateFrozenBitmapImage(pngBase64);
        return CreateDibBytes(image);
    }

    public static byte[] CreateDibBytes(BitmapSource image)
    {
        ArgumentNullException.ThrowIfNull(image);

        var encoder = new BmpBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(image));
        using var bitmapStream = new MemoryStream();
        encoder.Save(bitmapStream);

        var bitmapBytes = bitmapStream.ToArray();
        if (bitmapBytes.Length <= BitmapFileHeaderSize ||
            bitmapBytes[0] != (byte)'B' ||
            bitmapBytes[1] != (byte)'M')
        {
            throw new InvalidDataException("Invalid BMP bytes generated for clipboard DIB.");
        }

        return bitmapBytes[BitmapFileHeaderSize..];
    }

    public static string? TryCreateThumbnailPngBase64(string pngBase64, int decodePixelWidth)
    {
        try
        {
            var bytes = GetPngBytes(pngBase64);
            using var input = new MemoryStream(bytes);
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
            image.DecodePixelWidth = Math.Clamp(decodePixelWidth, 32, 512);
            image.StreamSource = input;
            image.EndInit();
            image.Freeze();

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(image));
            using var output = new MemoryStream();
            encoder.Save(output);
            return Convert.ToBase64String(output.ToArray());
        }
        catch
        {
            return null;
        }
    }

    private static string NormalizeBase64(string value)
    {
        var trimmed = value.Trim();
        var commaIndex = trimmed.IndexOf(',', StringComparison.Ordinal);
        if (trimmed.StartsWith("data:", StringComparison.OrdinalIgnoreCase) && commaIndex >= 0)
        {
            trimmed = trimmed[(commaIndex + 1)..].Trim();
        }

        return trimmed.Any(char.IsWhiteSpace)
            ? new string(trimmed.Where(character => !char.IsWhiteSpace(character)).ToArray())
            : trimmed;
    }
}
