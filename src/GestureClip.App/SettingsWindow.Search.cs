using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;
using GestureClip.App.Controls;
using GestureClip.App.Services;
using GestureClip.App.ViewModels;
using GestureClip.Core.Gestures;
using WpfButton = System.Windows.Controls.Button;
using WpfButtonBase = System.Windows.Controls.Primitives.ButtonBase;
using WpfHorizontalAlignment = System.Windows.HorizontalAlignment;
using WpfKey = System.Windows.Input.Key;
using WpfKeyEventArgs = System.Windows.Input.KeyEventArgs;
using WpfRadioButton = System.Windows.Controls.RadioButton;
using WpfScrollBar = System.Windows.Controls.Primitives.ScrollBar;
using WpfSelector = System.Windows.Controls.Primitives.Selector;
using WpfTextBoxBase = System.Windows.Controls.Primitives.TextBoxBase;
using WpfThickness = System.Windows.Thickness;
using WpfVisibility = System.Windows.Visibility;
using WpfFontWeights = System.Windows.FontWeights;
using WpfBrush = System.Windows.Media.Brush;

namespace GestureClip.App;

public partial class SettingsWindow
{
    private sealed record SettingsSearchEntry(string Title, string Path, string Keywords, string PageKey, string? TargetElementName = null);

    private static readonly SettingsSearchEntry[] SearchIndex =
    [
        new("总览", "首页", "总览 首页 状态 权限", "home", "HomePageIntro"),
        new("智能粘贴", "首页 / 智能粘贴", "智能粘贴 推荐 纯文本 净化 Ctrl+V 粘贴", "home", "SmartPasteRecommendationCard"),
        new("深色模式", "首页 / 系统信息", "深色 外观 主题 夜间 模式", "home", "SectionSystemInfo"),
        new("系统信息", "首页 / 系统信息", "权限 数据库 日志 运行状态", "home", "SectionSystemInfo"),
        new("剪贴板记录", "剪贴板 / 剪贴板记录", "剪贴板 记录 历史 捕获 启用 开关", "clipboard", "SectionClipboardCapture"),
        new("打开历史热键", "剪贴板 / 快捷键", "快捷键 热键 历史 Ctrl 打开", "clipboard", "SectionClipboardHotkeys"),
        new("快捷动作热键", "剪贴板 / 快捷键", "快捷动作 热键 Ctrl Shift Q", "clipboard", "SectionClipboardHotkeys"),
        new("纯文本粘贴热键", "剪贴板 / 快捷键", "纯文本 粘贴 Ctrl Shift V 去格式", "clipboard", "SectionClipboardHotkeys"),
        new("历史数据与清理", "剪贴板 / 历史数据与清理", "清理 历史 数量 保留 清空 危险", "clipboard", "SectionClipboardCleanup"),
        new("清空历史", "剪贴板 / 历史数据与清理", "清空 全部历史 非固定 危险操作", "clipboard", "SectionClipboardCleanup"),
        new("鼠标手势", "手势 / 手势基础", "手势 右键 画线 触发 开关 覆盖层", "gestures", "SectionGestureBasics"),
        new("边缘增强", "手势 / 边缘增强", "边缘 热区 角落 触发 停留", "gestures", "SectionEdgeEnhancement"),
        new("触发方式", "手势 / 高级", "右键 中键 侧键 触发方式", "gestures", "GestureAdvancedSettingsExpander"),
        new("屏幕角落和边缘", "手势 / 边缘完整设置", "角落 边缘 滑动 完整设置", "gestures", "EdgeTriggerSettingsGroup"),
        new("动作绑定", "动作绑定", "动作 绑定 设计 更换 预设", "bindings", "SectionBindingsIntro"),
        new("左键增强", "动作绑定 / 左键增强", "左键增强 手势码 R+L 增强动作 智能粘贴", "bindings", "SectionLeftButtonEnhanced"),
                new("常用话术", "动作绑定 / 话术", "话术 日期 时间 插入 模板语", "bindings", "SectionGestureSnippets"),
        new("手势配置导入导出", "动作绑定 / 模板", "导入 导出 备份 模板 办公 浏览", "bindings", "SectionGestureConfigTransfer"),
new("推荐手势", "动作绑定 / 推荐", "推荐 手势 一键应用", "bindings", "SectionRecommendedGestures"),
        new("修改选中手势", "动作绑定 / 修改", "更换动作 选中 修改", "bindings", "SectionGestureEditor"),
        new("设计手势", "动作绑定 / 设计", "自定义 手势 设计 录制 画", "bindings", "SectionGestureDesigner"),
        new("隐私与屏蔽名单", "隐私", "隐私 屏蔽 黑名单 进程", "privacy", "PrivacyPageIntro"),
        new("添加进程", "隐私 / 添加进程", "添加 进程名 exe 屏蔽", "privacy", "SectionPrivacyAdd"),
        new("屏蔽名单", "隐私 / 屏蔽名单", "名单 删除 剪贴板 手势", "privacy", "SectionPrivacyList"),
        new("开机启动", "自启", "自启 开机 启动 托盘 登录 Windows", "startup", "SectionStartup"),
        new("工位小熊", "小熊", "小熊 工位 收入 下班 摸鱼 统计", "workbear", "WorkstationPageIntro"),
        new("诊断", "诊断", "诊断 日志 复制 排查 信息", "diagnostics", "DiagnosticsPageIntro"),
        new("关于", "关于", "关于 版本 更新 检查 日志", "about", "AboutPageIntro"),
        new("检查更新", "关于 / 更新", "更新 检查 一键 覆盖 发版", "about", "AboutPageIntro"),
    ];

    private void SettingsSearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        UpdateSearchPlaceholder();
        RefreshSearchResults();
    }

    private void SettingsSearchBox_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        UpdateSearchPlaceholder();
        if (!string.IsNullOrWhiteSpace(SettingsSearchBox.Text))
        {
            RefreshSearchResults();
        }
    }

    private void SettingsSearchBox_PreviewKeyDown(object sender, WpfKeyEventArgs e)
    {
        if (e.Key == WpfKey.Escape)
        {
            ClearSearchButton_Click(sender, e);
            e.Handled = true;
            return;
        }

        if (e.Key == WpfKey.Enter && SearchResultsPanel.Children.OfType<WpfButton>().FirstOrDefault() is { Tag: var tag })
        {
            if (tag is SettingsSearchEntry entry)
            {
                NavigateToPage(entry.PageKey, entry.TargetElementName);
            }
            else if (tag is string page)
            {
                NavigateToPage(page);
            }

            e.Handled = true;
        }
    }

    private void ClearSearchButton_Click(object sender, RoutedEventArgs e)
    {
        SettingsSearchBox.Text = string.Empty;
        CloseSearchResults();
        UpdateSearchPlaceholder();
        SettingsSearchBox.Focus();
    }

    private void UpdateSearchPlaceholder()
    {
        var hasText = !string.IsNullOrEmpty(SettingsSearchBox.Text);
        SearchPlaceholder.Visibility = hasText ? WpfVisibility.Collapsed : WpfVisibility.Visible;
        ClearSearchButton.Visibility = hasText ? WpfVisibility.Visible : WpfVisibility.Collapsed;
    }

    private void RefreshSearchResults()
    {
        SearchResultsPanel.Children.Clear();
        var q = (SettingsSearchBox.Text ?? string.Empty).Trim();
        if (q.Length == 0)
        {
            CloseSearchResults();
            return;
        }

        var hits = SearchIndex
            .Select(entry => (entry, score: ScoreSearch(entry, q)))
            .Where(x => x.score > 0)
            .OrderByDescending(x => x.score)
            .ThenBy(x => x.entry.Title)
            .Take(8)
            .ToList();

        if (hits.Count == 0)
        {
            SearchResultsPanel.Children.Add(new TextBlock
            {
                Text = "未找到匹配的设置项",
                Margin = new WpfThickness(10, 8, 10, 8),
                Foreground = (WpfBrush)FindResource("BrushTextMuted"),
                FontSize = 12
            });
            SearchResultsPopup.IsOpen = true;
            return;
        }

        foreach (var (entry, _) in hits)
        {
            var btn = new WpfButton
            {
                Style = (Style)FindResource("SearchResultItemStyle"),
                Tag = entry,
                HorizontalContentAlignment = WpfHorizontalAlignment.Stretch
            };
            var stack = new StackPanel();
            stack.Children.Add(new TextBlock
            {
                Text = entry.Title,
                FontSize = 13,
                FontWeight = WpfFontWeights.SemiBold,
                Foreground = (WpfBrush)FindResource("BrushTextPrimary")
            });
            stack.Children.Add(new TextBlock
            {
                Text = entry.Path,
                FontSize = 11,
                Margin = new WpfThickness(0, 3, 0, 0),
                Foreground = (WpfBrush)FindResource("BrushTextMuted")
            });
            btn.Content = stack;
            btn.Click += SearchResult_Click;
            SearchResultsPanel.Children.Add(btn);
        }

        SearchResultsPopup.IsOpen = true;
    }

    private static int ScoreSearch(SettingsSearchEntry entry, string query)
    {
        var q = query.Trim();
        if (q.Length == 0)
        {
            return 0;
        }

        var blob = $"{entry.Title} {entry.Path} {entry.Keywords}";
        var compact = blob.Replace(" ", string.Empty).Replace("·", string.Empty).Replace("/", string.Empty);
        var qCompact = q.Replace(" ", string.Empty);

        if (blob.Contains(q, StringComparison.OrdinalIgnoreCase)
            || compact.Contains(qCompact, StringComparison.OrdinalIgnoreCase))
        {
            var score = 10;
            if (entry.Title.Contains(q, StringComparison.OrdinalIgnoreCase)
                || entry.Title.Replace(" ", string.Empty).Contains(qCompact, StringComparison.OrdinalIgnoreCase))
            {
                score += 20;
            }

            if (entry.Title.StartsWith(q, StringComparison.OrdinalIgnoreCase))
            {
                score += 15;
            }

            return score;
        }

        var parts = q.Split([' ', '/', '·', '，', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length > 1 && parts.All(p => blob.Contains(p, StringComparison.OrdinalIgnoreCase)))
        {
            return 8;
        }

        return 0;
    }

    private void SearchResult_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: SettingsSearchEntry entry })
        {
            NavigateToPage(entry.PageKey, entry.TargetElementName);
        }
        else if (sender is FrameworkElement { Tag: string page })
        {
            NavigateToPage(page);
        }
    }

    private void CloseSearchResults()
    {
        if (SearchResultsPopup is not null)
        {
            SearchResultsPopup.IsOpen = false;
        }

        SearchResultsPanel?.Children.Clear();
    }

}
