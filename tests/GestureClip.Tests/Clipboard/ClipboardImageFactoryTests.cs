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
}
