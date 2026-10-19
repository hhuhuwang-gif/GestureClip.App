using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using GestureClip.Core.Abstractions;
using GestureClip.Core.Clipboard;
using GestureClip.Features.Assistant;
using GestureClip.Features.Clipboard;

namespace GestureClip.App.ViewModels;


public sealed partial class ClipboardOverlayViewModel
{
    private async Task LoadSelectedImagePreviewAsync(ClipboardItem item)
    {
        var version = Interlocked.Increment(ref _selectedImagePreviewVersion);
        try
        {
            var fullItem = await _clipboardService.GetByIdAsync(item.Id, CancellationToken.None);
            if (fullItem is null || version != Volatile.Read(ref _selectedImagePreviewVersion))
            {
                return;
            }

            var previewContent = await GetImagePreviewContentAsync(fullItem, cancellationToken: CancellationToken.None);
            if (string.IsNullOrWhiteSpace(previewContent) || SelectedItem?.Id != item.Id)
            {
                return;
            }

            UpdateItem(item.Id, current => current with
            {
                TextContent = current.IsImage ? null : current.TextContent,
                ThumbnailContent = previewContent
            });
        }
        catch (Exception ex)
        {
            ErrorMessage = $"加载图片预览失败：{ex.Message}";
        }
    }

    private static bool NeedsImagePreview(ClipboardItem? item)
    {
        return item is not null &&
            item.IsImage &&
            string.IsNullOrWhiteSpace(item.ThumbnailContent);
    }

    private static async Task<string?> GetImagePreviewContentAsync(ClipboardItem item, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(item.ThumbnailContent))
        {
            return item.ThumbnailContent;
        }

        if (!item.IsImage || string.IsNullOrWhiteSpace(item.TextContent))
        {
            return item.TextContent;
        }

        var thumbnail = await Task.Run(
            () => GestureClip.Infrastructure.Clipboard.ClipboardImageFactory.TryCreateThumbnailPngBase64(
                item.TextContent,
                SelectedImagePreviewPixelWidth),
            cancellationToken);
        if (!string.IsNullOrWhiteSpace(thumbnail))
        {
            return thumbnail;
        }

        return EstimateBase64DecodedByteCount(item.TextContent) <= MaxInlineImagePreviewBytes
            ? item.TextContent
            : null;
    }

    private static long EstimateBase64DecodedByteCount(string value)
    {
        var base64 = NormalizeBase64(value);
        if (base64.Length == 0)
        {
            return 0;
        }

        var padding = base64.EndsWith("==", StringComparison.Ordinal)
            ? 2
            : base64.EndsWith("=", StringComparison.Ordinal)
                ? 1
                : 0;
        return (base64.Length * 3L / 4L) - padding;
    }

    private static string NormalizeBase64(string value)
    {
        var trimmed = value.Trim();
        var commaIndex = trimmed.IndexOf(',', StringComparison.Ordinal);
        if (trimmed.StartsWith("data:", StringComparison.OrdinalIgnoreCase) && commaIndex >= 0)
        {
            trimmed = trimmed[(commaIndex + 1)..].Trim();
        }

        return trimmed.Any(char.IsWhiteSpace)
            ? new string(trimmed.Where(character => !char.IsWhiteSpace(character)).ToArray())
            : trimmed;
    }

}
