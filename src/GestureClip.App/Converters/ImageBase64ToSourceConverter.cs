using System.Globalization;
using System.IO;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace GestureClip.App.Converters;

public sealed class ImageBase64ToSourceConverter : IValueConverter
{
    private const int MaxCachedImages = 128;
    /// <summary>Chars sampled from each end of the raw base64 for the cache key, so a
    /// cache hit never scans the full (possibly multi-MB) string.</summary>
    private const int CacheKeySampleLength = 64;
    private const int DefaultDecodePixelWidth = 320;
    private const int MinDecodePixelWidth = 32;
    private const int MaxDecodePixelWidth = 720;
    private static readonly object CacheLock = new();
    private static readonly Dictionary<string, BitmapImage> Cache = [];
    private static readonly Queue<string> CacheOrder = [];

    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not string rawBase64 || string.IsNullOrWhiteSpace(rawBase64))
        {
            return null;
        }

        try
        {
            var decodePixelWidth = GetDecodePixelWidth(parameter);
            var cacheKey = CreateCacheKey(rawBase64, decodePixelWidth);
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

    private static bool TryGetCached(string key, out BitmapImage image)
    {
        lock (CacheLock)
        {
            return Cache.TryGetValue(key, out image!);
        }
    }

    private static void AddToCache(string key, BitmapImage image)
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

    private static string CreateCacheKey(string rawBase64, int decodePixelWidth)
    {
        // Length + head/tail samples identify an image without touching the middle of the
        // payload; hashing the full string used to cost O(n) on every cache hit.
        var head = rawBase64[..Math.Min(CacheKeySampleLength, rawBase64.Length)];
        var tail = rawBase64[Math.Max(0, rawBase64.Length - CacheKeySampleLength)..];
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{decodePixelWidth}:{rawBase64.Length}:{head.GetHashCode(StringComparison.Ordinal):X8}:{tail.GetHashCode(StringComparison.Ordinal):X8}");
    }
}
