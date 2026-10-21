using GestureClip.Core.Abstractions;
using GestureClip.Features.Assistant;
using GestureClip.Features.Gestures;
using Microsoft.Extensions.Logging;

namespace GestureClip.Features.Clipboard;

public sealed class PlainTextPasteService : IPlainTextPasteService
{
    private static readonly TimeSpan CaptureSuppressionDuration = TimeSpan.FromMilliseconds(1500);

    private readonly IClipboardService _clipboardService;
    private readonly IClipboardTextReader _clipboardTextReader;
    private readonly IClipboardWriter _clipboardWriter;
    private readonly IPasteTargetService _pasteTargetService;
    private readonly ILogger<PlainTextPasteService> _logger;

    public PlainTextPasteService(
        PlainTextPasteDependencies dependencies,
        IPasteTargetService pasteTargetService,
        ILogger<PlainTextPasteService> logger)
    {
        _clipboardService = dependencies.ClipboardService;
        _clipboardTextReader = dependencies.TextReader;
        _clipboardWriter = dependencies.Writer;
        _pasteTargetService = pasteTargetService;
        _logger = logger;
    }

    public async Task PastePlainTextAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var target = _pasteTargetService.CaptureTarget();
        await _pasteTargetService.WaitUntilReadyAsync(target, cancellationToken);
        var text = _clipboardTextReader.TryReadText();
        if (string.IsNullOrEmpty(text))
        {
            _logger.LogInformation("Plain text rewrite skipped (empty text); sending normal paste hotkey.");
            await _clipboardWriter.SendPasteHotkeyAsync(target.WindowHandle, cancellationToken);
            return;
        }

        var plain = LocalTextTransforms.ToPlainText(text);
        plain = SmartPastePolicy.CleanText(plain);
        if (string.IsNullOrEmpty(plain))
        {
            _logger.LogInformation("Plain text cleaned to empty; sending normal paste hotkey.");
            await _clipboardWriter.SendPasteHotkeyAsync(target.WindowHandle, cancellationToken);
            return;
        }

        _clipboardService.SuppressCaptureFor(CaptureSuppressionDuration);
        await _clipboardWriter.SetTextAsync(plain, cancellationToken);
        await _clipboardWriter.SendPasteHotkeyAsync(target.WindowHandle, cancellationToken);
        _logger.LogInformation("Plain text paste completed. Length={Length}", plain.Length);
    }
}
