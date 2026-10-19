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
    public async Task<bool> CopySelectedAsPlainTextAsync(IReadOnlyList<ClipboardItem> selectedItems)
    {
        if (selectedItems.Count == 0)
        {
            return false;
        }

        if (selectedItems.Any(item => item.IsImage))
        {
            StatusText = "图片无法复制为纯文本，请改用「复制」";
            return false;
        }

        try
        {
            ErrorMessage = null;
            var plainItems = await Task.Run(() => CollectPlainItemsAsync(selectedItems));

            if (plainItems.Count == 0)
            {
                StatusText = "没有可复制的纯文本";
                return false;
            }

            await _clipboardService.CopyItemsAsync(plainItems, CancellationToken.None);
            MarkItemsUsed(plainItems.Select(item => item.Id).ToArray());
            StatusText = plainItems.Count == 1
                ? "已复制为纯文本 · 面板未关闭"
                : $"已合并复制 {plainItems.Count} 条纯文本 · 面板未关闭";
            return true;
        }
        catch (NotSupportedException ex)
        {
            StatusText = ex.Message;
            return false;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"纯文本复制失败：{ex.Message}";
            StatusText = "纯文本复制失败";
            return false;
        }
    }

    /// <summary>
    /// Copies the selected items to the clipboard as real files (text → .txt, image → .png),
    /// so they can be pasted straight into Explorer. Files land in the local exports folder.
    /// </summary>
    public async Task<bool> CopySelectedAsFilesAsync(IReadOnlyList<ClipboardItem> selectedItems)
    {
        if (selectedItems.Count == 0)
        {
            return false;
        }

        try
        {
            ErrorMessage = null;
            var files = await Task.Run(() => ExportItemsAsync(selectedItems));

            if (files.Count == 0)
            {
                StatusText = "没有可导出为文件的内容";
                return false;
            }

            var dropList = new System.Collections.Specialized.StringCollection();
            dropList.AddRange([.. files]);
            System.Windows.Clipboard.SetFileDropList(dropList);
            MarkItemsUsed(selectedItems.Select(item => item.Id).ToArray());
            StatusText = files.Count == 1
                ? "已以文件形式复制，可直接粘贴到文件夹"
                : $"已以 {files.Count} 个文件复制，可直接粘贴到文件夹";
            return true;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"以文件复制失败：{ex.Message}";
            StatusText = "以文件复制失败";
            return false;
        }
    }

    private static string CleanPlainText(string text)
    {
        var normalized = text
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace("\r", "\n", StringComparison.Ordinal);
        var lines = normalized.Split('\n');
        var result = new List<string>(lines.Length);
        var blankCount = 0;

        foreach (var line in lines)
        {
            var cleanedLine = line.TrimEnd();
            if (cleanedLine.Length == 0)
            {
                blankCount++;
                if (blankCount <= 1)
                {
                    result.Add("");
                }

                continue;
            }

            blankCount = 0;
            result.Add(cleanedLine);
        }

        while (result.Count > 0 && result[0].Length == 0)
        {
            result.RemoveAt(0);
        }

        while (result.Count > 0 && result[^1].Length == 0)
        {
            result.RemoveAt(result.Count - 1);
        }

        return string.Join("\r\n", result);
    }

}
