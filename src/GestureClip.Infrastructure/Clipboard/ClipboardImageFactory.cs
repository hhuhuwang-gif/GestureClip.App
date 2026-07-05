using System.IO;
using System.Windows.Media.Imaging;

namespace GestureClip.Infrastructure.Clipboard;

public static class ClipboardImageFactory
{
    public static BitmapImage CreateFrozenBitmapImage(string pngBase64)
    {
        var bytes = Convert.FromBase64String(NormalizeBase64(pngBase64));
        using var stream = new MemoryStream(bytes);
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
        image.StreamSource = stream;
        image.EndInit();
        image.Freeze();
        return image;
    }

    public static string? TryCreateThumbnailPngBase64(string pngBase64, int decodePixelWidth)
    {
        try
        {
            var bytes = Convert.FromBase64String(NormalizeBase64(pngBase64));
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
