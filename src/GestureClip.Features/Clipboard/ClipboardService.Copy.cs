using System.Diagnostics;
using GestureClip.Core.Clipboard;
using GestureClip.Features.Assistant;
using Microsoft.Extensions.Logging;

namespace GestureClip.Features.Clipboard;

public sealed partial class ClipboardService
{
    public async Task CopyItemsAsync(IReadOnlyList<ClipboardItem> items, CancellationToken cancellationToken)
    {
        var copyWatch = Stopwatch.StartNew();
        if (items.Count == 0)
        {
            return;
        }

        var hasText = items.Any(item => item.IsText);
        var hasImage = items.Any(item => item.IsImage);
        if (hasText && hasImage)
        {
            throw new NotSupportedException(
                "不能同时选文字和图片一起复制。请只选文本（可多条合并），或只选一张图片。");
        }

        SuppressCaptureFor(TimeSpan.FromMilliseconds(1000));
        if (hasImage)
        {
            if (items.Count > 1)
            {
                throw new NotSupportedException(
                    "一次只能复制一张图片。请取消多余选中，只保留一张再复制。");
            }

            var image = await EnsureFullContentAsync(items[0], cancellationToken);
            if (string.IsNullOrWhiteSpace(image.TextContent))
            {
                return;
            }

            RecordInternalWrite(imageHash: _clipboardHashService.ComputeHash(image.TextContent));
            await _clipboardWriter.SetImagePngBase64Async(image.TextContent, cancellationToken);
            RecordUseCountInBackground([image.Id]);
            copyWatch.Stop();
            LogPerf("ClipboardCopyDurationMs", copyWatch.ElapsedMilliseconds, ("ContentType", "image/png"), ("ItemCount", items.Count));
            _logger.LogInformation("Clipboard image item copied.");
            return;
        }

        await CopyTextItemsAsync(items, copyWatch, cancellationToken);
    }

    private async Task CopyTextItemsAsync(IReadOnlyList<ClipboardItem> items, Stopwatch copyWatch, CancellationToken cancellationToken)
    {
        var textItems = new List<ClipboardItem>(items.Count);
        foreach (var item in items)
        {
            textItems.Add(await EnsureFullContentAsync(item, cancellationToken));
        }

        // History re-copy always writes Unicode plain text so target apps (chat/IDE)
        // never receive leftover HTML/Markdown fragments as the only useful payload.
        var text = string.Join(
            "\r\n",
            textItems
                .Where(item => !string.IsNullOrEmpty(item.TextContent))
                .Select(item => item.IsFavorite && SnippetTemplate.ContainsVariables(item.TextContent)
                    ? SnippetTemplate.Expand(item.TextContent!, DateTimeOffset.Now)
                    : item.TextContent!)
                .Select(LocalTextTransforms.ToPlainText));
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        RecordInternalWrite(textHash: _clipboardHashService.ComputeHash(text));
        await _clipboardWriter.SetTextAsync(text, cancellationToken);
        RecordUseCountInBackground(textItems.Select(item => item.Id).ToArray());
        copyWatch.Stop();
        LogPerf("ClipboardCopyDurationMs", copyWatch.ElapsedMilliseconds, ("ContentType", "text"), ("ItemCount", items.Count));

        _logger.LogInformation("Clipboard text items copied. Count={ClipboardItemCount}", items.Count);
    }

}
