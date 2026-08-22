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
    public void TryEncodeImageAsPngBase64_converts_jpeg_bytes()
    {
        var source = ClipboardImageFactory.CreateFrozenBitmapImage(OnePixelPngBase64);
        var encoder = new System.Windows.Media.Imaging.JpegBitmapEncoder();
        encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(source));
        using var jpeg = new MemoryStream();
        encoder.Save(jpeg);

        var base64 = ClipboardImageDataReader.TryEncodeImageAsPngBase64(jpeg.ToArray());

        Assert.NotNull(base64);
        var bytes = Convert.FromBase64String(base64);
        Assert.True(bytes.Take(4).SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47 }));
    }

    [Fact]
    public void TryEncodeDibAsPngBase64_supports_bitmap_v5_header()
    {
        var dibV5 = new byte[128];
        BitConverter.GetBytes(124).CopyTo(dibV5, 0);
        BitConverter.GetBytes(1).CopyTo(dibV5, 4);
        BitConverter.GetBytes(1).CopyTo(dibV5, 8);
        BitConverter.GetBytes((ushort)1).CopyTo(dibV5, 12);
        BitConverter.GetBytes((ushort)32).CopyTo(dibV5, 14);
        BitConverter.GetBytes(4).CopyTo(dibV5, 20);
        dibV5[124] = 0;
        dibV5[125] = 0;
        dibV5[126] = 255;
        dibV5[127] = 255;

        var base64 = ClipboardImageDataReader.TryEncodeDibAsPngBase64(dibV5);

        Assert.NotNull(base64);
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
        Assert.Contains("DibV5ClipboardFormatId", source);
        Assert.Contains("EncodedImageFormats", source);
        Assert.Contains("Clipboard.ContainsFileDropList()", source);
        Assert.Contains("Clipboard.ContainsData(System.Windows.DataFormats.Dib)", source);
        Assert.Contains("Clipboard.ContainsImage()", source);
        Assert.Contains("ClipboardStaDispatcher", source);
        Assert.DoesNotContain("Application.Current.Dispatcher", source);
    }


    [Fact]
    public void WpfClipboardTextReader_captures_snapshot_before_encoding_image_data()
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

        Assert.Contains("CaptureImageSnapshot", source);
        Assert.Contains("EncodeImageSnapshot", source);
        Assert.DoesNotContain("return TryReadRawPngBase64()", source);
        Assert.DoesNotContain("?? TryReadDibPngBase64()", source);
    }


    [Fact]
    public void WpfClipboardTextReader_short_circuits_image_snapshot_formats()
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
        var methodStart = source.IndexOf("private ImageClipboardSnapshot CaptureImageSnapshot()", StringComparison.Ordinal);
        var methodEnd = source.IndexOf("private string? EncodeImageSnapshot", StringComparison.Ordinal);
        var method = source[methodStart..methodEnd];

        Assert.Contains("if (rawPngBytes is not null)", method);
        Assert.Contains("if (dibBytes is not null)", method);
        Assert.Contains("return new ImageClipboardSnapshot(rawPngBytes", method);
        Assert.Contains("return new ImageClipboardSnapshot(null, dibBytes", method);
        Assert.DoesNotContain("TryReadRawPngBytes(),", method);
    }

    [Fact]
    public void WpfClipboardWriter_uses_background_sta_dispatcher_instead_of_ui_dispatcher()
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
            "WpfClipboardWriter.cs"));
        var source = File.ReadAllText(sourcePath);

        Assert.Contains("ClipboardStaDispatcher", source);
        Assert.DoesNotContain("Application.Current.Dispatcher", source);
    }

    [Fact]
    public void WpfClipboardWriter_writes_png_bitmap_and_dib_formats_for_images()
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
            "WpfClipboardWriter.cs"));
        var source = File.ReadAllText(sourcePath);

        Assert.Contains("SetDataObject(dataObject, copy: true)", source);
        Assert.Contains("dataObject.SetImage", source);
        Assert.Contains("dataObject.SetData(\"PNG\"", source);
        Assert.Contains("DataFormats.Dib", source);
    }

    [Fact]
    public void ClipboardStaDispatcher_runs_clipboard_work_on_background_sta_thread()
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
            "ClipboardStaDispatcher.cs"));
        var source = File.ReadAllText(sourcePath);

        Assert.Contains("IsBackground = true", source);
        Assert.Contains("SetApartmentState(ApartmentState.STA)", source);
        Assert.Contains("Dispatcher.Run()", source);
        Assert.Contains("BeginInvokeShutdown", source);
    }

    private const string OnePixelPngBase64 =
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/p9sAAAAASUVORK5CYII=";
}

