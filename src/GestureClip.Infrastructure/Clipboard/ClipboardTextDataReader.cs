using System.IO;
using System.Text;

namespace GestureClip.Infrastructure.Clipboard;

public static class ClipboardTextDataReader
{
    public static string? TryGetText(object? data, string? format = null)
    {
        try
        {
            var text = data switch
            {
                null => null,
                string value => value,
                char[] characters => new string(characters),
                byte[] bytes => DecodeBytes(bytes, format),
                MemoryStream memory => DecodeBytes(memory.ToArray(), format),
                Stream stream => DecodeBytes(ReadStream(stream), format),
                _ => data.ToString()
            };

            return string.IsNullOrWhiteSpace(text)
                ? null
                : text.TrimEnd('\0');
        }
        catch
        {
            return null;
        }
    }

    private static string DecodeBytes(byte[] bytes, string? format)
    {
        if (bytes.Length == 0)
        {
            return "";
        }

        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
        {
            return Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
        }

        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
        {
            return Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2);
        }

        if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
        {
            return Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2);
        }

        if (format?.Contains("Unicode", StringComparison.OrdinalIgnoreCase) == true || LooksLikeUtf16Le(bytes))
        {
            return Encoding.Unicode.GetString(bytes);
        }

        try
        {
            return new UTF8Encoding(false, true).GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            return Encoding.Default.GetString(bytes);
        }
    }

    private static bool LooksLikeUtf16Le(byte[] bytes)
    {
        if (bytes.Length < 4 || bytes.Length % 2 != 0)
        {
            return false;
        }

        var pairs = Math.Min(bytes.Length / 2, 32);
        var zeroHighBytes = 0;
        for (var index = 0; index < pairs; index++)
        {
            if (bytes[(index * 2) + 1] == 0)
            {
                zeroHighBytes++;
            }
        }

        return zeroHighBytes >= Math.Max(2, pairs * 3 / 4);
    }

    private static byte[] ReadStream(Stream stream)
    {
        if (stream.CanSeek)
        {
            stream.Position = 0;
        }

        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    }
}
