using System.Diagnostics;
using GestureClip.Core.Clipboard;
using GestureClip.Core.Settings;
using GestureClip.Features.Assistant;
using GestureClip.Features.Gestures;
using Microsoft.Extensions.Logging;

namespace GestureClip.Features.Clipboard;

public sealed partial class ClipboardService
{
    public async Task PasteAsync(ClipboardItem item, PasteOptions options, CancellationToken cancellationToken)
    {
        var pasteWatch = Stopwatch.StartNew();
        if (item.IsImage)
        {
            var image = await EnsureFullContentAsync(item, cancellationToken);
            if (string.IsNullOrWhiteSpace(image.TextContent))
            {
                throw new InvalidOperationException("图片内容不存在，未执行粘贴。");
            }

            SuppressCaptureFor(TimeSpan.FromMilliseconds(1500));
            RecordInternalWrite(imageHash: _clipboardHashService.ComputeHash(image.TextContent));
            await _clipboardWriter.SetImagePngBase64Async(image.TextContent, cancellationToken);
            await _clipboardWriter.SendPasteHotkeyAsync(options.TargetWindowHandle, cancellationToken);
            await RecordPasteUsageAsync(image.Id, DateTimeOffset.UtcNow, cancellationToken);
            pasteWatch.Stop();
            LogPerf("PasteMs", pasteWatch.ElapsedMilliseconds, ("ContentType", "image/png"));
            return;
        }

        var textItem = await EnsureFullContentAsync(item, cancellationToken);
        if (string.IsNullOrEmpty(textItem.TextContent))
        {
            throw new InvalidOperationException("文字内容不存在，未执行粘贴。");
        }

        var pasteText = textItem.TextContent;
        if (textItem.IsFavorite && SnippetTemplate.ContainsVariables(pasteText))
        {
            pasteText = SnippetTemplate.Expand(pasteText, DateTimeOffset.Now);
        }

        if (options.PlainText)
        {
            pasteText = LocalTextTransforms.ToPlainText(pasteText);
        }
        else if (_settingsService.Get(SettingKeys.SmartPasteEnabled, true))
        {
            var strategy = SmartPastePolicy.Select(_foregroundAppService.GetCurrent());
            pasteText = SmartPastePolicy.TransformForStrategy(pasteText, strategy);
        }

        SuppressCaptureFor(TimeSpan.FromMilliseconds(1500));
        RecordInternalWrite(textHash: _clipboardHashService.ComputeHash(pasteText));
        await _clipboardWriter.SetTextAsync(pasteText, cancellationToken);
        await _clipboardWriter.SendPasteHotkeyAsync(options.TargetWindowHandle, cancellationToken);
        await RecordPasteUsageAsync(textItem.Id, DateTimeOffset.UtcNow, cancellationToken);
        pasteWatch.Stop();
        LogPerf("PasteMs", pasteWatch.ElapsedMilliseconds, ("ContentType", "text"));
        _logger.LogInformation("Clipboard text item pasted.");
    }

}
