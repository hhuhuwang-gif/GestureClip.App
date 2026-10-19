using GestureClip.Core.Clipboard;

namespace GestureClip.App.ViewModels;

public sealed partial class ClipboardOverlayViewModel
{
    private async Task<IReadOnlyList<ClipboardItem>> CollectPlainItemsAsync(IReadOnlyList<ClipboardItem> selectedItems)
    {
        var plainItems = new List<ClipboardItem>(selectedItems.Count);
        foreach (var item in selectedItems)
        {
            var full = await ResolveFullItemAsync(item);
            var raw = full.TextContent ?? item.TextContent ?? item.PreviewText ?? "";
            var plain = CleanPlainText(raw);
            if (string.IsNullOrEmpty(plain))
            {
                continue;
            }

            plainItems.Add(full with
            {
                ContentType = "text",
                TextContent = plain,
                PreviewText = plain.Length > TextPreviewLength ? plain[..TextPreviewLength] : plain
            });
        }
        return plainItems;
    }

    private async Task<IReadOnlyList<string>> ExportItemsAsync(IReadOnlyList<ClipboardItem> selectedItems)
    {
        var exportDir = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "GestureClip",
            "exports");
        System.IO.Directory.CreateDirectory(exportDir);
        var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var files = new List<string>();
        var index = 0;
        foreach (var item in selectedItems)
        {
            var full = await ResolveFullItemAsync(item);
            index++;
            var suffix = selectedItems.Count > 1 ? $"_{index}" : "";
            if (full.IsImage && !string.IsNullOrEmpty(full.TextContent))
            {
                var path = System.IO.Path.Combine(exportDir, $"剪贴板_{stamp}{suffix}.png");
                await System.IO.File.WriteAllBytesAsync(
                    path,
                    GestureClip.Infrastructure.Clipboard.ClipboardImageFactory.GetPngBytes(full.TextContent));
                files.Add(path);
            }
            else
            {
                var text = full.TextContent ?? full.PreviewText;
                if (string.IsNullOrEmpty(text))
                {
                    continue;
                }

                var path = System.IO.Path.Combine(exportDir, $"剪贴板_{stamp}{suffix}.txt");
                await System.IO.File.WriteAllTextAsync(path, text);
                files.Add(path);
            }
        }
        return files;
    }

    private async Task<ClipboardItem> ResolveFullItemAsync(ClipboardItem item)
    {
        if (!string.IsNullOrWhiteSpace(item.TextContent))
        {
            return item;
        }
        return await _clipboardService.GetByIdAsync(item.Id, CancellationToken.None) ?? item;
    }
}
