using System.Globalization;
using System.IO;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace GestureClip.App.Converters;

public sealed class ImageBase64ToSourceConverter : IValueConverter
{
    // Thumbnails only ever render at ~120-160 px in the history list, so a wider decode buys
    // nothing and costs ~4 bytes per pixel of retained bitmap. 64 entries keeps the hot set
    // (one screen plus a little scrollback) cached while roughly halving resident memory.
    private const int MaxCachedImages = 64;
    private const int CacheKeySampleLength = 64;
    private const int DefaultDecodePixelWidth = 256;
    private const int MinDecodePixelWidth = 32;
    private const int MaxDecodePixelWidth = 720;
    private static readonly object CacheLock = new();
    private static readonly Dictionary<ImageCacheKey, BitmapImage> Cache = [];
    private static readonly Queue<ImageCacheKey> CacheOrder = [];

    private sealed record ImageCacheKey(string Base64, int DecodePixelWidth)
    {
        public override int GetHashCode()
        {
            var sampleLength = Math.Min(CacheKeySampleLength, Base64.Length);
            var head = Base64.AsSpan(0, sampleLength);
            var tail = Base64.AsSpan(Base64.Length - sampleLength);
            return HashCode.Combine(DecodePixelWidth, Base64.Length,
                string.GetHashCode(head, StringComparison.Ordinal),
                string.GetHashCode(tail, StringComparison.Ordinal));
        }
    }

    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not string rawBase64 || string.IsNullOrWhiteSpace(rawBase64))
        {
            return null;
        }

        try
        {
            var decodePixelWidth = GetDecodePixelWidth(parameter);
            var cacheKey = new ImageCacheKey(rawBase64, decodePixelWidth);
            if (TryGetCached(cacheKey, out var cached))
            {
                return cached;
            }

            var base64 = NormalizeBase64(rawBase64);
            var bytes = System.Convert.FromBase64String(base64);
            using var stream = new MemoryStream(bytes);
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
            image.StreamSource = stream;
            image.DecodePixelWidth = decodePixelWidth;
            image.EndInit();
            image.Freeze();
            AddToCache(cacheKey, image);
            return image;
        }
        catch
        {
            return null;
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
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

    private static int GetDecodePixelWidth(object parameter)
    {
        var configured = parameter switch
        {
            int intValue => intValue,
            string stringValue when int.TryParse(stringValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) => parsed,
            _ => DefaultDecodePixelWidth
        };

        return Math.Clamp(configured, MinDecodePixelWidth, MaxDecodePixelWidth);
    }

    private static bool TryGetCached(ImageCacheKey key, out BitmapImage image)
    {
        lock (CacheLock)
        {
            return Cache.TryGetValue(key, out image!);
        }
    }

    private static void AddToCache(ImageCacheKey key, BitmapImage image)
    {
        lock (CacheLock)
        {
            if (Cache.ContainsKey(key))
            {
                return;
            }

            Cache[key] = image;
            CacheOrder.Enqueue(key);
            while (Cache.Count > MaxCachedImages && CacheOrder.TryDequeue(out var oldestKey))
            {
                Cache.Remove(oldestKey);
            }
        }
    }

}
