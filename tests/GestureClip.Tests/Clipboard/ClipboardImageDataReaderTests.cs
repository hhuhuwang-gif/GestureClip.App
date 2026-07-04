using GestureClip.Infrastructure.Clipboard;
using Xunit;

namespace GestureClip.Tests.Clipboard;

public sealed class ClipboardImageDataReaderTests
{
    [Fact]
    public void TryEncodeDibAsPngBase64_converts_bitmap_info_to_png()
    {
        var dib = new byte[]
        {
            40, 0, 0, 0,
            1, 0, 0, 0,
            1, 0, 0, 0,
            1, 0,
            32, 0,
            0, 0, 0, 0,
            4, 0, 0, 0,
            19, 11, 0, 0,
            19, 11, 0, 0,
            0, 0, 0, 0,
            0, 0, 0, 0,
            0, 0, 255, 255
        };

        var base64 = ClipboardImageDataReader.TryEncodeDibAsPngBase64(dib);

        Assert.NotNull(base64);
        var bytes = Convert.FromBase64String(base64);
        Assert.True(bytes.Take(4).SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47 }));
    }

    [Fact]
    public void TryGetPngBase64_returns_png_stream_without_reencoding()
    {
        var png = Convert.FromBase64String(OnePixelPngBase64);
        using var stream = new MemoryStream(png);

        var base64 = ClipboardImageDataReader.TryGetPngBase64(stream);

        Assert.Equal(OnePixelPngBase64, base64);
    }

    [Fact]
    public void WpfClipboardTextReader_checks_image_formats_before_reading_heavy_data()
    {
        var sourcePath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "..",
            "src",
            "GestureClip.Infrastructure",
            "Clipboard",
            "WpfClipboardTextReader.cs"));
        var source = File.ReadAllText(sourcePath);

        Assert.Contains("Clipboard.ContainsData(\"PNG\")", source);
        Assert.Contains("Clipboard.ContainsData(System.Windows.DataFormats.Dib)", source);
        Assert.Contains("Clipboard.ContainsImage()", source);
    }

    private const string OnePixelPngBase64 =
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/p9sAAAAASUVORK5CYII=";
}
