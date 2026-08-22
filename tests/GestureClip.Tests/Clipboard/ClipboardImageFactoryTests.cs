using GestureClip.Infrastructure.Clipboard;
using Xunit;

namespace GestureClip.Tests.Clipboard;

public sealed class ClipboardImageFactoryTests
{
    private const string OnePixelPngBase64 =
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/p9sAAAAASUVORK5CYII=";

    [Fact]
    public void CreateFrozenBitmapImage_returns_frozen_bitmap()
    {
        var image = ClipboardImageFactory.CreateFrozenBitmapImage(OnePixelPngBase64);

        Assert.True(image.IsFrozen);
        Assert.True(image.PixelWidth > 0);
        Assert.True(image.PixelHeight > 0);
    }

    [Fact]
    public void GetPngBytes_returns_png_signature_bytes()
    {
        var bytes = ClipboardImageFactory.GetPngBytes(OnePixelPngBase64);

        Assert.True(bytes.Take(4).SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47 }));
    }

    [Fact]
    public void CreateDibBytes_returns_clipboard_dib_without_bitmap_file_header()
    {
        var dib = ClipboardImageFactory.CreateDibBytes(OnePixelPngBase64);

        Assert.True(dib.Length > 40);
        Assert.Equal(40, BitConverter.ToInt32(dib, 0));
        Assert.False(dib[0] == (byte)'B' && dib[1] == (byte)'M');
    }

    [Fact]
    public void TryFitPngBase64_downscales_large_png_under_limit()
    {
        var large = CreateNoisyPngBase64(512, 512);
        const int maxBytes = 128 * 1024;
        Assert.True(Convert.FromBase64String(large).Length > maxBytes);

        var fitted = ClipboardImageFactory.TryFitPngBase64(large, maxBytes);

        Assert.NotNull(fitted);
        Assert.True(Convert.FromBase64String(fitted).Length <= maxBytes);
        Assert.NotNull(ClipboardImageFactory.CreateFrozenBitmapImage(fitted));
    }

    [Fact]
    public void TryCreateThumbnailPngBase64_returns_decodable_thumbnail()
    {
        var thumbnail = ClipboardImageFactory.TryCreateThumbnailPngBase64(OnePixelPngBase64, 96);

        Assert.False(string.IsNullOrWhiteSpace(thumbnail));
        var image = ClipboardImageFactory.CreateFrozenBitmapImage(thumbnail!);
        Assert.True(image.IsFrozen);
        Assert.True(image.PixelWidth > 0);
    }

    [Fact]
    public void TryCreateThumbnailPngBase64_returns_null_for_invalid_image()
    {
        var thumbnail = ClipboardImageFactory.TryCreateThumbnailPngBase64("not-image", 96);

        Assert.Null(thumbnail);
    }

    private static string CreateNoisyPngBase64(int width, int height)
    {
        var pixels = new byte[width * height * 4];
        new Random(42).NextBytes(pixels);
        for (var index = 3; index < pixels.Length; index += 4)
        {
            pixels[index] = 255;
        }

        var bitmap = System.Windows.Media.Imaging.BitmapSource.Create(
            width,
            height,
            96,
            96,
            System.Windows.Media.PixelFormats.Bgra32,
            null,
            pixels,
            width * 4);
        var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
        encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
        using var output = new MemoryStream();
        encoder.Save(output);
        return Convert.ToBase64String(output.ToArray());
    }
}
