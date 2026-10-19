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
    public string ShortcutHintText { get; private set; } = DefaultShortcutHint;

    public bool CanUndoDelete => _undoDeleteItems.Count > 0;

    public bool IsShortcutHelpVisible
    {
        get => _isShortcutHelpVisible;
        private set
        {
            if (_isShortcutHelpVisible == value)
            {
                return;
            }

            _isShortcutHelpVisible = value;
            OnPropertyChanged();
        }
    }

    public string ShortcutHelpText { get; } =
        """
        打开/关闭面板    Ctrl + `
        聚焦搜索         Ctrl + F
        分类筛选         Ctrl + 1~6
        复制             Ctrl + C
        纯文本复制       Ctrl + Shift + C
        粘贴并关闭       Enter
        粘贴不关闭       Ctrl + Enter
        纯文本粘贴       Shift + Enter
        纯文本粘贴不关   Ctrl + Shift + Enter
        复制为文件       Ctrl + Shift + F
        切换分类         Ctrl + Tab / Ctrl + Shift + Tab
        编辑选中记录     Ctrl + E
        数字快捷粘贴     1~9/0（列表聚焦）或 Alt+数字
        置顶 / 片段      Ctrl + P / Ctrl + S
        删除             Delete
        撤销删除         Ctrl + Z（刚删过时）
        回顶 / 到底      Home / End
        重置视图         Esc（有搜索时）或「清空」
        快捷键速查       ? 或 F1

        搜索技巧：
        · 拼音首字母搜索（如 wx 找「微信」）
        · re: 开头使用正则搜索（如 re:\d{6}）
        · 搜索时 ↑/↓ 直接切换选中项

        左侧色条：蓝=文本 紫=图片 橙=置顶 绿=本会话已从历史复制
        多选复制时，绿条可区分哪些已复制、哪些还没动。
        右键文本记录可用「文本工具」：大小写 / 去空白 / JSON / URL 解码。
        """;

    public string EmptyStateText
    {
        get => _emptyStateText;
        private set
        {
            _emptyStateText = value;
            OnPropertyChanged();
        }
    }

    public string StatusText
    {
        get => _statusText;
        private set
        {
            _statusText = value;
            OnPropertyChanged();
        }
    }

    /// <summary>Lets the window surface view-side failures (e.g. pin window creation) in the status bar.</summary>
    public void SetStatusText(string text)
    {
        StatusText = text;
    }

    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            if (_isLoading == value)
            {
                return;
            }

            _isLoading = value;
            OnPropertyChanged();
        }
    }

    public bool IsLoadingMore
    {
        get => _isLoadingMore;
        private set
        {
            if (_isLoadingMore == value)
            {
                return;
            }

            _isLoadingMore = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CanLoadMore));
        }
    }

    public bool HasMoreItems
    {
        get => _hasMoreItems;
        private set
        {
            if (_hasMoreItems == value)
            {
                return;
            }

            _hasMoreItems = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CanLoadMore));
        }
    }

    public bool CanLoadMore => HasMoreItems && !IsLoading && !IsLoadingMore;

    public string? ErrorMessage
    {
        get => _errorMessage;
        private set
        {
            if (_errorMessage == value)
            {
                return;
            }

            _errorMessage = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasError));
        }
    }

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

}
