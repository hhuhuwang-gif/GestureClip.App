using GestureClip.Core.Abstractions;
using GestureClip.Features.Assistant;
using GestureClip.Features.Gestures;
using Microsoft.Extensions.Logging;

namespace GestureClip.Features.Clipboard;

public sealed class PlainTextPasteService : IPlainTextPasteService
{
    private static readonly TimeSpan ClipboardSettleDelay = TimeSpan.FromMilliseconds(70);

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
            // No text (maybe image-only clipboard): still try a normal paste hotkey.
            _logger.LogInformation("Plain text rewrite skipped (empty text); sending normal paste hotkey.");
            await _clipboardWriter.SendPasteHotkeyAsync(cancellationToken);
            return;
        }

        var plain = LocalTextTransforms.ToPlainText(text);
        plain = SmartPastePolicy.CleanText(plain);
        if (string.IsNullOrEmpty(plain))
        {
            _logger.LogInformation("Plain text cleaned to empty; sending normal paste hotkey.");
            await _clipboardWriter.SendPasteHotkeyAsync(cancellationToken);
            return;
        }

        _clipboardService.SuppressCaptureFor(TimeSpan.FromMilliseconds(1500));
        await _clipboardWriter.SetTextAsync(plain, cancellationToken);
        // Wait for clipboard to settle before Ctrl+V (and for user to release physical keys).
        await Task.Delay(ClipboardSettleDelay, cancellationToken);
        await _clipboardWriter.SendPasteHotkeyAsync(cancellationToken);
        _logger.LogInformation("Plain text paste completed. Length={Length}", plain.Length);
    }
}
