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
    public bool IsSnippetEditorOpen
    {
        get => _isSnippetEditorOpen;
        private set
        {
            if (_isSnippetEditorOpen == value)
            {
                return;
            }

            _isSnippetEditorOpen = value;
            OnPropertyChanged();
        }
    }

    public string NewSnippetText
    {
        get => _newSnippetText;
        set
        {
            if (_newSnippetText == value)
            {
                return;
            }

            _newSnippetText = value;
            OnPropertyChanged();
        }
    }

    public void OpenSnippetEditor()
    {
        NewSnippetText = "";
        IsSnippetEditorOpen = true;
    }

    public void CloseSnippetEditor()
    {
        IsSnippetEditorOpen = false;
    }

    public async Task<bool> SaveSnippetAsync()
    {
        var item = await _clipboardService.AddSnippetAsync(NewSnippetText, CancellationToken.None);
        if (item is null)
        {
            StatusText = "片段内容不能为空";
            return false;
        }

        IsSnippetEditorOpen = false;
        StatusText = "片段已保存 · 粘贴时 {date} {time} 等变量自动展开";
        return true;
    }

    public bool IsItemEditorOpen
    {
        get => _isItemEditorOpen;
        private set
        {
            if (_isItemEditorOpen == value)
            {
                return;
            }

            _isItemEditorOpen = value;
            OnPropertyChanged();
        }
    }

    public string EditingItemText
    {
        get => _editingItemText;
        set
        {
            if (_editingItemText == value)
            {
                return;
            }

            _editingItemText = value;
            OnPropertyChanged();
        }
    }

    public async Task<bool> OpenItemEditorAsync(ClipboardItem? item)
    {
        if (item is null)
        {
            return false;
        }

        if (!item.IsText)
        {
            StatusText = "只有文本记录可以编辑";
            return false;
        }

        var full = await _clipboardService.GetByIdAsync(item.Id, CancellationToken.None) ?? item;
        _editingItemId = full.Id;
        EditingItemText = full.TextContent ?? full.PreviewText ?? "";
        IsItemEditorOpen = true;
        return true;
    }

    public void CloseItemEditor()
    {
        IsItemEditorOpen = false;
        _editingItemId = null;
    }

    public async Task<bool> SaveItemEditAsync()
    {
        if (_editingItemId is not { } id)
        {
            return false;
        }

        var updated = await _clipboardService.UpdateTextContentAsync(id, EditingItemText, CancellationToken.None);
        if (updated is null)
        {
            StatusText = "内容不能为空，或该记录不可编辑";
            return false;
        }

        for (var index = 0; index < Items.Count; index++)
        {
            if (Items[index].Id == id)
            {
                Items[index] = updated with { IsSessionCopied = Items[index].IsSessionCopied };
                break;
            }
        }

        CloseItemEditor();
        StatusText = "已保存修改";
        return true;
    }

    public bool IsQrOpen
    {
        get => _isQrOpen;
        private set
        {
            if (_isQrOpen == value)
            {
                return;
            }

            _isQrOpen = value;
            OnPropertyChanged();
        }
    }

    public System.Windows.Media.ImageSource? QrImageSource
    {
        get => _qrImageSource;
        private set
        {
            if (ReferenceEquals(_qrImageSource, value))
            {
                return;
            }

            _qrImageSource = value;
            OnPropertyChanged();
        }
    }

    public async Task<bool> ShowQrForItemAsync(ClipboardItem? item)
    {
        if (item is null)
        {
            return false;
        }

        if (!item.IsText)
        {
            StatusText = "只有文本记录可以生成二维码";
            return false;
        }

        var full = await _clipboardService.GetByIdAsync(item.Id, CancellationToken.None) ?? item;
        var text = full.TextContent ?? full.PreviewText ?? "";
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        if (text.Length > 1800)
        {
            StatusText = "内容超过 1800 字符，二维码装不下";
            return false;
        }

        try
        {
            ErrorMessage = null;
            var pngBytes = await Task.Run(() =>
            {
                using var generator = new QRCoder.QRCodeGenerator();
                using var qrData = generator.CreateQrCode(text, QRCoder.QRCodeGenerator.ECCLevel.Q);
                return new QRCoder.PngByteQRCode(qrData).GetGraphic(10);
            });

            var image = new System.Windows.Media.Imaging.BitmapImage();
            image.BeginInit();
            image.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
            image.StreamSource = new System.IO.MemoryStream(pngBytes);
            image.EndInit();
            image.Freeze();
            QrImageSource = image;
            IsQrOpen = true;
            return true;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"二维码生成失败：{ex.Message}";
            StatusText = "二维码生成失败";
            return false;
        }
    }

    public void CloseQr()
    {
        IsQrOpen = false;
        QrImageSource = null;
    }

    /// <summary>Fetches the full image item for pinning to desktop; sets status when not applicable.</summary>
    public async Task<ClipboardItem?> PreparePinImageAsync(ClipboardItem? item)
    {
        if (item is null)
        {
            return null;
        }

        if (!item.IsImage)
        {
            StatusText = "只有图片记录可以贴到桌面";
            return null;
        }

        var full = await _clipboardService.GetByIdAsync(item.Id, CancellationToken.None) ?? item;
        if (string.IsNullOrEmpty(full.TextContent))
        {
            StatusText = "图片内容读取失败";
            return null;
        }

        StatusText = "已贴到桌面 · 拖动移动 · 滚轮缩放 · 双击关闭";
        return full;
    }

}
