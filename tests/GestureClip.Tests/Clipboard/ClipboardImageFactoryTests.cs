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
