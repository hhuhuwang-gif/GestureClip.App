using System.Runtime.InteropServices.WindowsRuntime;
using GestureClip.Core.Abstractions;
using Microsoft.Extensions.Logging;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage.Streams;

namespace GestureClip.Infrastructure.Clipboard;

public sealed class WindowsImageOcrService : IImageOcrService
{
    private readonly ILogger<WindowsImageOcrService> _logger;
    private readonly OcrEngine? _engine;

    public WindowsImageOcrService(ILogger<WindowsImageOcrService> logger)
    {
        _logger = logger;
        try
        {
            _engine = OcrEngine.TryCreateFromUserProfileLanguages()
                      ?? OcrEngine.TryCreateFromLanguage(new Windows.Globalization.Language("zh-Hans"))
                      ?? OcrEngine.TryCreateFromLanguage(new Windows.Globalization.Language("zh-CN"))
                      ?? OcrEngine.TryCreateFromLanguage(new Windows.Globalization.Language("en-US"));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Windows OCR engine unavailable.");
            _engine = null;
        }
    }

    public async Task<string> RecognizePngBase64Async(string? pngBase64, CancellationToken cancellationToken = default)
    {
        if (_engine is null || string.IsNullOrWhiteSpace(pngBase64))
        {
            return "";
        }

        try
        {
            var comma = pngBase64.IndexOf(',');
            var payload = comma >= 0 ? pngBase64[(comma + 1)..] : pngBase64;
            var bytes = Convert.FromBase64String(payload);
            if (bytes.Length < 32)
            {
                return "";
            }

            using var stream = new InMemoryRandomAccessStream();
            await stream.WriteAsync(bytes.AsBuffer());
            stream.Seek(0);

            var decoder = await BitmapDecoder.CreateAsync(stream);
            using var softwareBitmap = await decoder.GetSoftwareBitmapAsync(
                BitmapPixelFormat.Bgra8,
                BitmapAlphaMode.Premultiplied);

            var result = await _engine.RecognizeAsync(softwareBitmap);
            var text = result?.Text?.Trim() ?? "";
            if (text.Length > 8000)
            {
                text = text[..8000];
            }

            return text;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "OCR failed.");
            return "";
        }
    }
}
