using GestureClip.Core.Abstractions;
using GestureClip.Features.Assistant;
using GestureClip.Features.Gestures;
using Microsoft.Extensions.Logging;

namespace GestureClip.Features.Clipboard;

public sealed class PlainTextPasteService : IPlainTextPasteService
{
    private readonly IClipboardService _clipboardService;
    private readonly IClipboardTextReader _clipboardTextReader;
    private readonly IClipboardWriter _clipboardWriter;
    private readonly ILogger<PlainTextPasteService> _logger;

    public PlainTextPasteService(
        IClipboardService clipboardService,
        IClipboardTextReader clipboardTextReader,
        IClipboardWriter clipboardWriter,
        ILogger<PlainTextPasteService> logger)
    {
        _clipboardService = clipboardService;
        _clipboardTextReader = clipboardTextReader;
        _clipboardWriter = clipboardWriter;
        _logger = logger;
    }

    public async Task PastePlainTextAsync(CancellationToken cancellationToken = default)
    {
        var text = _clipboardTextReader.TryReadText();
        if (string.IsNullOrEmpty(text))
        {
            _logger.LogInformation("Plain text paste skipped: empty clipboard.");
            return;
        }

        var plain = LocalTextTransforms.ToPlainText(text);
        plain = SmartPastePolicy.CleanText(plain);
        if (string.IsNullOrEmpty(plain))
        {
            _logger.LogInformation("Plain text paste skipped: cleaned text empty.");
            return;
        }

        _clipboardService.SuppressCaptureFor(TimeSpan.FromMilliseconds(1200));
        await _clipboardWriter.SetTextAsync(plain, cancellationToken);
        await _clipboardWriter.SendPasteHotkeyAsync(cancellationToken);
        _logger.LogInformation("Plain text paste completed. Length={Length}", plain.Length);
    }
}
