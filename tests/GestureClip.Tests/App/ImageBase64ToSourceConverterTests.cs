using System.Globalization;
using System.Windows.Media.Imaging;
using GestureClip.App.Converters;
using Xunit;

namespace GestureClip.Tests.App;

public sealed class ImageBase64ToSourceConverterTests
{
    private const string OnePixelPngBase64 =
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/p9sAAAAASUVORK5CYII=";

    [Fact]
    public void Convert_returns_bitmap_for_png_base64()
    {
        var converter = new ImageBase64ToSourceConverter();

        var result = converter.Convert(OnePixelPngBase64, typeof(BitmapImage), new object(), CultureInfo.InvariantCulture);

        var image = Assert.IsAssignableFrom<BitmapImage>(result);
        Assert.True(image.IsFrozen);
        Assert.True(image.PixelWidth > 0);
        Assert.True(image.PixelHeight > 0);
    }

    [Fact]
    public void Convert_returns_bitmap_for_data_uri()
    {
        var converter = new ImageBase64ToSourceConverter();

        var result = converter.Convert(
            $"data:image/png;base64,{OnePixelPngBase64}",
            typeof(BitmapImage),
            new object(),
            CultureInfo.InvariantCulture);

        Assert.IsAssignableFrom<BitmapImage>(result);
    }

    [Fact]
    public void Convert_reuses_cached_bitmap_for_same_base64()
    {
        var converter = new ImageBase64ToSourceConverter();

        var first = converter.Convert(OnePixelPngBase64, typeof(BitmapImage), new object(), CultureInfo.InvariantCulture);
        var second = converter.Convert(OnePixelPngBase64, typeof(BitmapImage), new object(), CultureInfo.InvariantCulture);

        Assert.Same(first, second);
    }

    [Fact]
    public void Convert_returns_null_for_invalid_base64()
    {
        var converter = new ImageBase64ToSourceConverter();

        var result = converter.Convert("not-png", typeof(BitmapImage), new object(), CultureInfo.InvariantCulture);

        Assert.Null(result);
    }
}
