using System.IO;
using System.Windows.Media.Imaging;

namespace GestureClip.Infrastructure.Clipboard;

public static class ClipboardImageDataReader
{
    public static string? TryGetPngBase64(object? data)
    {
        try
        {
            var bytes = ToBytes(data);
            if (bytes is null || !LooksLikePng(bytes))
            {
                return null;
            }

            return Convert.ToBase64String(bytes);
        }
        catch
        {
            return null;
        }
    }

    public static string? TryEncodeImageAsPngBase64(object? data)
    {
        try
        {
            var bytes = ToBytes(data);
            if (bytes is null || bytes.Length == 0)
            {
                return null;
            }

            if (LooksLikePng(bytes))
            {
                return Convert.ToBase64String(bytes);
            }

            using var input = new MemoryStream(bytes);
            var decoder = BitmapDecoder.Create(
                input,
                BitmapCreateOptions.PreservePixelFormat,
                BitmapCacheOption.OnLoad);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(decoder.Frames[0]));
            using var output = new MemoryStream();
            encoder.Save(output);
            return Convert.ToBase64String(output.ToArray());
        }
        catch
        {
            return null;
        }
    }

    public static string? TryEncodeDibAsPngBase64(object? data)
    {
        try
        {
            var dibBytes = ToBytes(data);
            if (dibBytes is null || dibBytes.Length < 12)
            {
                return null;
            }

            var bmpBytes = CreateBmpBytesFromDib(dibBytes);
            using var bmpStream = new MemoryStream(bmpBytes);
            var decoder = new BmpBitmapDecoder(bmpStream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(decoder.Frames[0]));
            using var pngStream = new MemoryStream();
            encoder.Save(pngStream);
            return Convert.ToBase64String(pngStream.ToArray());
        }
        catch
        {
            return null;
        }
    }

    private static byte[]? ToBytes(object? data)
    {
        return data switch
        {
            null => null,
            byte[] bytes => bytes,
            MemoryStream memoryStream => memoryStream.ToArray(),
            Stream stream => ReadStream(stream),
            _ => null
        };
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

    private static bool LooksLikePng(byte[] bytes)
    {
        return bytes.Length >= 8 &&
            bytes[0] == 0x89 &&
            bytes[1] == 0x50 &&
            bytes[2] == 0x4E &&
            bytes[3] == 0x47 &&
            bytes[4] == 0x0D &&
            bytes[5] == 0x0A &&
            bytes[6] == 0x1A &&
            bytes[7] == 0x0A;
    }

    private static byte[] CreateBmpBytesFromDib(byte[] dibBytes)
    {
        var headerSize = BitConverter.ToInt32(dibBytes, 0);
        if (headerSize <= 0 || headerSize > dibBytes.Length)
        {
            throw new InvalidDataException("Invalid DIB header.");
        }

        ushort bitCount;
        var colorsUsed = 0;
        var paletteEntrySize = 4;
        var extraMaskBytes = 0;
        if (headerSize == 12)
        {
            bitCount = BitConverter.ToUInt16(dibBytes, 10);
            paletteEntrySize = 3;
        }
        else
        {
            if (headerSize < 40 || dibBytes.Length < 40)
            {
                throw new InvalidDataException("Unsupported DIB header.");
            }

            bitCount = BitConverter.ToUInt16(dibBytes, 14);
            colorsUsed = BitConverter.ToInt32(dibBytes, 32);
            var compression = BitConverter.ToInt32(dibBytes, 16);
            if (headerSize == 40)
            {
                extraMaskBytes = compression switch
                {
                    3 => 12, // BI_BITFIELDS
                    6 => 16, // BI_ALPHABITFIELDS
                    _ => 0
                };
            }
        }

        var paletteSize = GetPaletteSize(bitCount, colorsUsed, paletteEntrySize);
        var pixelOffset = 14 + headerSize + extraMaskBytes + paletteSize;
        if (pixelOffset > 14 + dibBytes.Length)
        {
            throw new InvalidDataException("DIB pixel offset exceeds payload.");
        }
        var fileSize = 14 + dibBytes.Length;

        using var stream = new MemoryStream(fileSize);
        using var writer = new BinaryWriter(stream);
        writer.Write((byte)'B');
        writer.Write((byte)'M');
        writer.Write(fileSize);
        writer.Write((ushort)0);
        writer.Write((ushort)0);
        writer.Write(pixelOffset);
        writer.Write(dibBytes);
        writer.Flush();
        return stream.ToArray();
    }

    private static int GetPaletteSize(ushort bitCount, int colorsUsed, int entrySize)
    {
        if (bitCount > 8)
        {
            return 0;
        }

        var colors = colorsUsed > 0 ? colorsUsed : 1 << bitCount;
        return checked(colors * entrySize);
    }
}
