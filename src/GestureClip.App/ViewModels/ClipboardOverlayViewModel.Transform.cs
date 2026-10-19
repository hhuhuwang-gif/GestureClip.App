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
    public async Task<bool> TransformSelectedTextAsync(ClipboardTextTransform transform)
    {
        if (SelectedItem is null) return false;
        if (!SelectedItem.IsText)
        {
            StatusText = "文本工具只支持文本记录";
            return false;
        }

        try
        {
            ErrorMessage = null;
            var full = await _clipboardService.GetByIdAsync(SelectedItem.Id, CancellationToken.None) ?? SelectedItem;
            var raw = full.TextContent ?? SelectedItem.TextContent ?? SelectedItem.PreviewText ?? "";
            if (string.IsNullOrEmpty(raw))
            {
                StatusText = "选中的记录没有文本内容";
                return false;
            }

            var result = TransformText(raw, transform);
            if (result is not { } transformed) return false;
            if (string.IsNullOrEmpty(transformed.Text))
            {
                StatusText = "转换结果为空，未复制";
                return false;
            }

            var transformedItem = full with
            {
                ContentType = "text",
                TextContent = transformed.Text,
                PreviewText = transformed.Text.Length > TextPreviewLength
                    ? transformed.Text[..TextPreviewLength] : transformed.Text
            };
            await _clipboardService.CopyItemsAsync([transformedItem], CancellationToken.None);
            MarkItemsUsed([full.Id]);
            StatusText = $"已复制转换结果（{transformed.Label}）· 面板未关闭";
            return true;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"文本转换失败：{ex.Message}";
            StatusText = "文本转换失败";
            return false;
        }
    }

    private (string Text, string Label)? TransformText(string raw, ClipboardTextTransform transform)
    {
        return transform switch
        {
            ClipboardTextTransform.UpperCase => (LocalTextTransforms.ToUpper(raw), "大写"),
            ClipboardTextTransform.LowerCase => (LocalTextTransforms.ToLower(raw), "小写"),
            ClipboardTextTransform.CompactWhitespace => (CleanPlainText(raw), "去多余空白"),
            ClipboardTextTransform.UrlDecode => (LocalTextTransforms.UrlDecode(raw), "URL 解码"),
            ClipboardTextTransform.FormatJson => TransformJson(raw, minify: false),
            ClipboardTextTransform.MinifyJson => TransformJson(raw, minify: true),
            _ => null
        };
    }

    private (string Text, string Label)? TransformJson(string raw, bool minify)
    {
        var success = minify
            ? LocalTextTransforms.TryMinifyJson(raw, out var text, out _)
            : LocalTextTransforms.TryFormatJson(raw, out text, out _);
        if (success) return (text, minify ? "JSON 压缩" : "JSON 格式化");
        StatusText = minify ? "不是有效的 JSON，无法压缩" : "不是有效的 JSON，无法格式化";
        return null;
    }

    public async Task<bool> ToggleSelectedPinnedAsync()
    {
        if (SelectedItem is null)
        {
            return false;
        }

        var nextPinned = !SelectedItem.IsPinned;
        var id = SelectedItem.Id;
        await _clipboardService.SetPinnedAsync(id, nextPinned, CancellationToken.None);
        UpdateItem(id, item => item with { IsPinned = nextPinned, UpdatedAt = DateTimeOffset.UtcNow });
        ApplyFilter();
        StatusText = nextPinned ? "已置顶" : "已取消置顶";
        return true;
    }

    public async Task<bool> ToggleSelectedFavoriteAsync()
    {
        if (SelectedItem is null)
        {
            return false;
        }

        var nextFavorite = !SelectedItem.IsFavorite;
        var id = SelectedItem.Id;
        await _clipboardService.SetFavoriteAsync(id, nextFavorite, CancellationToken.None);
        UpdateItem(id, item => item with { IsFavorite = nextFavorite, UpdatedAt = DateTimeOffset.UtcNow });
        ApplyFilter();
        StatusText = nextFavorite ? "已保存为片段" : "已取消片段";
        return true;
    }

}
