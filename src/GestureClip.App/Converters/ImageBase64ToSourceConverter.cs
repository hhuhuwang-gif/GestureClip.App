using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace GestureClip.App.Converters;

public sealed class ImageBase64ToSourceConverter : IValueConverter
{
    private const int MaxCachedImages = 96;
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
            var base64 = NormalizeBase64(rawBase64);
            var cacheKey = CreateCacheKey(base64);
            if (TryGetCached(cacheKey, out var cached))
            {
                return cached;
            }

            var bytes = System.Convert.FromBase64String(base64);
            using var stream = new MemoryStream(bytes);
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
            image.StreamSource = stream;
            image.DecodePixelWidth = 360;
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
            return trimmed[(commaIndex + 1)..].Trim();
        }

        return trimmed;
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

    private static string CreateCacheKey(string base64)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(base64));
        return $"{base64.Length}:{System.Convert.ToHexString(bytes)}";
    }
}
