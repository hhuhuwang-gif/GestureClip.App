using System.Globalization;
using System.Windows;
using System.Windows.Media;
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
    public void Convert_returns_bitmap_for_wrapped_base64()
    {
        var converter = new ImageBase64ToSourceConverter();
        var wrapped = OnePixelPngBase64.Insert(24, "\r\n").Insert(54, "\n");

        var result = converter.Convert(wrapped, typeof(BitmapImage), new object(), CultureInfo.InvariantCulture);

        Assert.IsAssignableFrom<BitmapImage>(result);
    }

    [Fact]
    public void Convert_reuses_cached_bitmap_for_same_base64_and_decode_width()
    {
        var converter = new ImageBase64ToSourceConverter();

        var first = converter.Convert(OnePixelPngBase64, typeof(BitmapImage), 96, CultureInfo.InvariantCulture);
        var second = converter.Convert(OnePixelPngBase64, typeof(BitmapImage), 96, CultureInfo.InvariantCulture);

        Assert.Same(first, second);
    }

    [Fact]
    public void Convert_uses_separate_cache_entries_for_different_decode_widths()
    {
        var converter = new ImageBase64ToSourceConverter();

        var thumbnail = converter.Convert(OnePixelPngBase64, typeof(BitmapImage), 96, CultureInfo.InvariantCulture);
        var preview = converter.Convert(OnePixelPngBase64, typeof(BitmapImage), 360, CultureInfo.InvariantCulture);

        Assert.NotSame(thumbnail, preview);
    }

    [Fact]
    public void Convert_returns_null_for_invalid_base64()
    {
        var converter = new ImageBase64ToSourceConverter();

        var result = converter.Convert("not-png", typeof(BitmapImage), new object(), CultureInfo.InvariantCulture);

        Assert.Null(result);
    }

    [Fact]
    public void Convert_distinguishes_images_with_equal_length_and_head_tail_samples()
    {
        const int sampleLength = 64;
        const int decodeWidth = 32;
        var firstBase64 = CreateBitmapWithCenterBlue(0);
        var secondBase64 = CreateBitmapWithCenterBlue(byte.MaxValue);
        Assert.Equal(firstBase64.Length, secondBase64.Length);
        Assert.Equal(firstBase64[..sampleLength], secondBase64[..sampleLength]);
        Assert.Equal(firstBase64[^sampleLength..], secondBase64[^sampleLength..]);
        Assert.NotEqual(firstBase64, secondBase64);
        var converter = new ImageBase64ToSourceConverter();

        var first = Assert.IsType<BitmapImage>(converter.Convert(
            firstBase64, typeof(BitmapImage), decodeWidth, CultureInfo.InvariantCulture));
        var second = Assert.IsType<BitmapImage>(converter.Convert(
            secondBase64, typeof(BitmapImage), decodeWidth, CultureInfo.InvariantCulture));

        Assert.NotSame(first, second);
        Assert.Equal(new byte[] { 0, 0, 0 }, ReadCenterPixel(first));
        Assert.Equal(new byte[] { byte.MaxValue, 0, 0 }, ReadCenterPixel(second));
        Assert.Same(second, converter.Convert(new string(secondBase64.ToCharArray()),
            typeof(BitmapImage), decodeWidth, CultureInfo.InvariantCulture));
    }

    private static string CreateBitmapWithCenterBlue(byte blue)
    {
        const int size = 32;
        const int channels = 3;
        const int dpi = 96;
        var stride = size * channels;
        var pixels = new byte[size * stride];
        pixels[(size / 2 * stride) + (size / 2 * channels)] = blue;
        var bitmap = BitmapSource.Create(size, size, dpi, dpi,
            PixelFormats.Bgr24, null, pixels, stride);
        var encoder = new BmpBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = new MemoryStream();
        encoder.Save(stream);
        return System.Convert.ToBase64String(stream.ToArray());
    }

    private static byte[] ReadCenterPixel(BitmapSource bitmap)
    {
        const int channels = 3;
        var converted = new FormatConvertedBitmap(bitmap, PixelFormats.Bgr24, null, 0);
        var pixel = new byte[channels];
        converted.CopyPixels(new Int32Rect(bitmap.PixelWidth / 2, bitmap.PixelHeight / 2, 1, 1),
            pixel, channels, 0);
        return pixel;
    }
}
