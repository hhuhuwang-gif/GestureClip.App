using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;
using GestureClip.App.Controls;
using GestureClip.Core.Gestures;
using GestureClip.App.Services;
using GestureClip.App.ViewModels;
using WpfButtonBase = System.Windows.Controls.Primitives.ButtonBase;
using WpfScrollBar = System.Windows.Controls.Primitives.ScrollBar;
using WpfSelector = System.Windows.Controls.Primitives.Selector;
using WpfTextBoxBase = System.Windows.Controls.Primitives.TextBoxBase;
using WpfRadioButton = System.Windows.Controls.RadioButton;
using WpfButton = System.Windows.Controls.Button;
using WpfHorizontalAlignment = System.Windows.HorizontalAlignment;
using WpfKeyEventArgs = System.Windows.Input.KeyEventArgs;
using WpfKey = System.Windows.Input.Key;
using WpfVisibility = System.Windows.Visibility;
using WpfThickness = System.Windows.Thickness;
using WpfFontWeights = System.Windows.FontWeights;
using WpfBrush = System.Windows.Media.Brush;

namespace GestureClip.App;

public partial class SettingsWindow : Window
{
    private const int GwlStyle = -16;
    private const int WsSysmenu = 0x00080000;
    private const int WsMinimizebox = 0x00020000;
    private const int WsThickframe = 0x00040000;
    private const int WsMaximizebox = 0x00010000;
    private const int WmNcHitTest = 0x0084;
    private const int HtClient = 1;
    private const int HtLeft = 10;
    private const int HtRight = 11;
    private const int HtTop = 12;
    private const int HtTopLeft = 13;
    private const int HtTopRight = 14;
    private const int HtBottom = 15;
    private const int HtBottomLeft = 16;
    private const int HtBottomRight = 17;
    private const int ResizeBorderThickness = 6;
    private HwndSource? _hwndSource;

    private readonly AppLifecycleService _appLifecycleService;
    private readonly List<GesturePoint> _recordedGesturePoints = [];
    private bool _isRecordingGesture;
    private bool _isRailCollapsed;
    private bool _suppressNavSync;
    private GridLength _savedRailWidth = new(240);

    public SettingsWindow(SettingsViewModel viewModel, AppLifecycleService appLifecycleService)
    {
        _appLifecycleService = appLifecycleService;
        InitializeComponent();
        DataContext = viewModel;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        EnableTaskbarMinimizeBehavior();
        EnableBorderlessResize();
        SyncNavSelection("home");
        UpdateSearchPlaceholder();
        // Ensure template names are available for rail chevron
        ToggleRailButton?.ApplyTemplate();
        UpdateRailToggleGlyph(collapsed: false);
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_appLifecycleService.IsExplicitExit)
        {
            e.Cancel = true;
            Hide();
            return;
        }

        base.OnClosing(e);
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            MaximizeButton_Click(sender, e);
            e.Handled = true;
            return;
        }

        try
        {
            DragMove();
            e.Handled = true;
        }
        catch (InvalidOperationException)
        {
        }
    }

    private void WindowBackground_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left || e.ClickCount != 1 || !CanDragFrom(e.OriginalSource as DependencyObject))
        {
            return;
        }

        try
        {
            DragMove();
            e.Handled = true;
        }
        catch (InvalidOperationException)
        {
        }
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void MaximizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private async void UpdateButton_Click(object sender, RoutedEventArgs e)
    {
        await _appLifecycleService.StartCoverUpdateAsync();
    }

    private async void CheckUpdateButton_Click(object sender, RoutedEventArgs e)
    {
        await _appLifecycleService.CheckForUpdatesAsync();
    }

    private void OpenReleaseButton_Click(object sender, RoutedEventArgs e)
    {
        _appLifecycleService.OpenLatestReleasePage();
    }

    private void ScrollToCustomGestureDesigner_Click(object sender, RoutedEventArgs e)
    {
        NavigateToPage("bindings", "SectionGestureDesigner");
        Dispatcher.BeginInvoke(new Action(() =>
        {
            try
            {
                var target = GestureDesignerPanel.TransformToAncestor(GestureBindingPageScrollViewer)
                    .Transform(new System.Windows.Point(0, 0));
                GestureBindingPageScrollViewer.ScrollToVerticalOffset(
                    Math.Max(0, GestureBindingPageScrollViewer.VerticalOffset + target.Y - 24));
            }
            catch (InvalidOperationException)
            {
                GestureBindingPageScrollViewer.ScrollToEnd();
            }
        }), System.Windows.Threading.DispatcherPriority.Loaded);
    }

    private void NavigateToBindings_Click(object sender, RoutedEventArgs e) => NavigateToPage("bindings");

    private void NavigateToEdgeEnhancement_Click(object sender, RoutedEventArgs e)
    {
        NavigateToPage("gestures", "SectionEdgeEnhancement");
        Dispatcher.BeginInvoke(new Action(() =>
        {
            GestureAdvancedSettingsExpander.IsExpanded = true;
            ScrollToNamedElement("EdgeTriggerSettingsGroup");
        }), System.Windows.Threading.DispatcherPriority.Loaded);
    }

    private void ExpandGestureAdvanced_Click(object sender, RoutedEventArgs e)
    {
        NavigateToPage("gestures", "GestureAdvancedSettingsExpander");
        Dispatcher.BeginInvoke(new Action(() =>
        {
            GestureAdvancedSettingsExpander.IsExpanded = true;
            ScrollToNamedElement("GestureAdvancedSettingsExpander");
        }), System.Windows.Threading.DispatcherPriority.Loaded);
    }

    private void FeatureMapCard_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string tag })
        {
            NavigateToPage(tag);
            e.Handled = true;
        }
    }

    private void ChangeGestureAction_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not SettingsViewModel viewModel)
        {
            return;
        }

        var pattern = (sender as FrameworkElement)?.Tag as string;
        if (string.IsNullOrWhiteSpace(pattern))
        {
            return;
        }

        viewModel.SelectedGestureBindingSelectionKey = pattern;
        NavigateToPage("bindings", "SectionGestureEditor");
        Dispatcher.BeginInvoke(new Action(() =>
        {
            try
            {
                ScrollToNamedElement("SectionGestureEditor");
                GestureActionEditorTitle.BringIntoView();
            }
            catch (InvalidOperationException)
            {
                GestureBindingPageScrollViewer.ScrollToVerticalOffset(
                    Math.Max(0, GestureBindingPageScrollViewer.ScrollableHeight * 0.45));
            }
        }), System.Windows.Threading.DispatcherPriority.Loaded);
        e.Handled = true;
    }

    /// <summary>
    /// Open a settings tab by header keyword / alias. Safe to call after show.
    /// </summary>
    public void NavigateToPage(string page, string? targetElementName = null)
    {
        if (MainSettingsTabControl is null)
        {
            return;
        }

        var key = NormalizePageKey(page);
        var header = PageHeaderFromKey(key);

        foreach (var item in MainSettingsTabControl.Items)
        {
            if (item is TabItem tab && string.Equals(tab.Header?.ToString(), header, StringComparison.Ordinal))
            {
                MainSettingsTabControl.SelectedItem = tab;
                break;
            }
        }

        SyncNavSelection(key);
        CloseSearchResults();

        if (!string.IsNullOrWhiteSpace(targetElementName))
        {
            Dispatcher.BeginInvoke(new Action(() => ScrollToNamedElement(targetElementName!)),
                System.Windows.Threading.DispatcherPriority.Loaded);
        }
    }

    private void ScrollToNamedElement(string elementName)
    {
        var target = FindName(elementName) as FrameworkElement;
        if (target is null)
        {
            return;
        }

        // Expand parent Expander if any
        var parent = target as DependencyObject;
        while (parent is not null)
        {
            if (parent is Expander expander)
            {
                expander.IsExpanded = true;
            }

            parent = System.Windows.Media.VisualTreeHelper.GetParent(parent);
        }

        try
        {
            target.BringIntoView();
        }
        catch (InvalidOperationException)
        {
            // ignore if not in visual tree yet
        }
    }

    private static string NormalizePageKey(string? page)
    {
        var key = (page ?? "").Trim().ToLowerInvariant();
        return key switch
        {
            "home" or "首页" or "" => "home",
            "clipboard" or "剪贴板" or "smartpaste" or "智能粘贴" => "clipboard",
            "gestures" or "手势" or "edge" or "边缘" => "gestures",
            "bindings" or "动作" or "动作绑定" or "绑定" or "designer" => "bindings",
            "privacy" or "隐私" or "数据" => "privacy",
            "startup" or "自启" => "startup",
            "workbear" or "小熊" or "工位" => "workbear",
            "diagnostics" or "诊断" => "diagnostics",
            "about" or "关于" => "about",
            _ => "home"
        };
    }

    private static string PageHeaderFromKey(string key) => key switch
    {
        "home" => "首页",
        "clipboard" => "剪贴板",
        "gestures" => "手势",
        "bindings" => "动作绑定",
        "privacy" => "隐私",
        "startup" => "自启",
        "workbear" => "小熊",
        "diagnostics" => "诊断",
        "about" => "关于",
        _ => "首页"
    };

    private void SyncNavSelection(string key)
    {
        _suppressNavSync = true;
        try
        {
            WpfRadioButton? target = key switch
            {
                "home" => NavHome,
                "clipboard" => NavClipboard,
                "gestures" => NavGestures,
                "bindings" => NavBindings,
                "privacy" => NavPrivacy,
                "startup" => NavStartup,
                "workbear" => NavWorkbear,
                "diagnostics" => NavDiagnostics,
                "about" => NavAbout,
                _ => NavHome
            };

            if (target is null)
            {
                return;
            }

            if (target.IsChecked != true)
            {
                target.IsChecked = true;
            }
        }
        finally
        {
            _suppressNavSync = false;
        }
    }



    private void MainSettingsTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressNavSync)
        {
            return;
        }

        if (MainSettingsTabControl?.SelectedItem is not TabItem tab)
        {
            return;
        }

        var header = tab.Header?.ToString() ?? "首页";
        var key = NormalizePageKey(header);
        SyncNavSelection(key);
    }

    private void NavItem_Checked(object sender, RoutedEventArgs e)
    {
        if (_suppressNavSync)
        {
            return;
        }

        if (sender is not WpfRadioButton { Tag: string tag } || string.IsNullOrWhiteSpace(tag))
        {
            return;
        }

        // Avoid re-entrancy while SyncNavSelection sets IsChecked
        if (MainSettingsTabControl?.SelectedItem is TabItem current
            && string.Equals(current.Header?.ToString(), PageHeaderFromKey(NormalizePageKey(tag)), StringComparison.Ordinal))
        {
            return;
        }

        NavigateToPage(tag);
    }

    private void ToggleRailButton_Click(object sender, RoutedEventArgs e)
    {
        if (RailColumn is null || RightRailBorder is null)
        {
            return;
        }

        if (_isRailCollapsed)
        {
            RailColumn.Width = _savedRailWidth.Value > 0 ? _savedRailWidth : new GridLength(240);
            RightRailBorder.Visibility = WpfVisibility.Visible;
            _isRailCollapsed = false;
            ToggleRailButton.ToolTip = "折叠右侧栏";
            ToggleRailButton.Content = null; // template text is chevron
            UpdateRailToggleGlyph(collapsed: false);
        }
        else
        {
            if (RailColumn.Width.IsAbsolute && RailColumn.Width.Value > 0)
            {
                _savedRailWidth = RailColumn.Width;
            }
            else if (!RailColumn.Width.IsAbsolute)
            {
                _savedRailWidth = new GridLength(240);
            }

            RailColumn.Width = new GridLength(0);
            RightRailBorder.Visibility = WpfVisibility.Collapsed;
            _isRailCollapsed = true;
            ToggleRailButton.ToolTip = "展开右侧栏";
            UpdateRailToggleGlyph(collapsed: true);
        }
    }

    private void UpdateRailToggleGlyph(bool collapsed)
    {
        if (ToggleRailButton is null)
        {
            return;
        }

        ToggleRailButton.ApplyTemplate();
        if (ToggleRailButton.Template?.FindName("Chevron", ToggleRailButton) is System.Windows.Controls.TextBlock chevron)
        {
            chevron.Text = collapsed ? "›" : "‹";
        }
    }

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

    private void RecordGesturePad_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement element || DataContext is not SettingsViewModel)
        {
            return;
        }

        _isRecordingGesture = true;
        _recordedGesturePoints.Clear();
        RecordGesturePolyline.Points.Clear();
        element.CaptureMouse();
        AddRecordedGesturePoint(element, e.GetPosition(element));
        e.Handled = true;
    }

    private void RecordGesturePad_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (!_isRecordingGesture || sender is not FrameworkElement element)
        {
            return;
        }

        AddRecordedGesturePoint(element, e.GetPosition(element));
        e.Handled = true;
    }

    private void RecordGesturePad_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isRecordingGesture || sender is not FrameworkElement element)
        {
            return;
        }

        AddRecordedGesturePoint(element, e.GetPosition(element));
        _isRecordingGesture = false;
        element.ReleaseMouseCapture();
        if (DataContext is SettingsViewModel viewModel)
        {
            viewModel.SetNewGesturePatternFromRecordedPoints(_recordedGesturePoints);
        }

        e.Handled = true;
    }

    private void AddRecordedGesturePoint(FrameworkElement element, System.Windows.Point point)
    {
        var x = (int)Math.Round(Math.Clamp(point.X, 0, element.ActualWidth));
        var y = (int)Math.Round(Math.Clamp(point.Y, 0, element.ActualHeight));
        var now = DateTimeOffset.UtcNow;
        if (_recordedGesturePoints.Count > 0)
        {
            var previous = _recordedGesturePoints[^1];
            var dx = previous.X - x;
            var dy = previous.Y - y;
            if (Math.Sqrt(dx * dx + dy * dy) < 3)
            {
                return;
            }
        }

        _recordedGesturePoints.Add(new GesturePoint(x, y, now));
        RecordGesturePolyline.Points.Add(new System.Windows.Point(x, y));
    }

    private void EnableTaskbarMinimizeBehavior()
    {
        var handle = new WindowInteropHelper(this).Handle;
        if (handle == IntPtr.Zero)
        {
            return;
        }

        var style = GetWindowLong(handle, GwlStyle);
        SetWindowLong(handle, GwlStyle, style | WsSysmenu | WsMinimizebox | WsMaximizebox | WsThickframe);
    }

    private void EnableBorderlessResize()
    {
        _hwndSource = PresentationSource.FromVisual(this) as HwndSource;
        _hwndSource?.AddHook(WndProc);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        const int wmEnterSizeMove = 0x0231;
        const int wmExitSizeMove = 0x0232;

        if (msg == wmEnterSizeMove)
        {
            SetWindowShadowEnabled(false);
            return IntPtr.Zero;
        }

        if (msg == wmExitSizeMove)
        {
            SetWindowShadowEnabled(true);
            return IntPtr.Zero;
        }

        if (msg != WmNcHitTest || WindowState == WindowState.Maximized)
        {
            return IntPtr.Zero;
        }

        var screenX = (short)(lParam.ToInt64() & 0xFFFF);
        var screenY = (short)((lParam.ToInt64() >> 16) & 0xFFFF);

        System.Windows.Point point;
        try
        {
            point = PointFromScreen(new System.Windows.Point(screenX, screenY));
        }
        catch
        {
            return IntPtr.Zero;
        }

        var width = ActualWidth;
        var height = ActualHeight;
        if (width <= 0 || height <= 0)
        {
            return IntPtr.Zero;
        }

        var border = ResizeBorderThickness;
        var left = point.X >= 0 && point.X <= border;
        var right = point.X >= width - border && point.X <= width + 1;
        var top = point.Y >= 0 && point.Y <= border;
        var bottom = point.Y >= height - border && point.Y <= height + 1;

        var hit = 0;
        if (top && left) hit = HtTopLeft;
        else if (top && right) hit = HtTopRight;
        else if (bottom && left) hit = HtBottomLeft;
        else if (bottom && right) hit = HtBottomRight;
        else if (left) hit = HtLeft;
        else if (right) hit = HtRight;
        else if (top) hit = HtTop;
        else if (bottom) hit = HtBottom;
        else return IntPtr.Zero;

        handled = true;
        return new IntPtr(hit);
    }

    private void SetWindowShadowEnabled(bool enabled)
    {
        Border? chrome = Content as Border;
        if (chrome is null && VisualTreeHelper.GetChildrenCount(this) > 0)
        {
            chrome = VisualTreeHelper.GetChild(this, 0) as Border;
        }

        if (chrome is null)
        {
            return;
        }

        if (enabled)
        {
            chrome.Effect ??= new DropShadowEffect
            {
                Color = Colors.Black,
                BlurRadius = 16,
                ShadowDepth = 2,
                Opacity = 0.08,
                Direction = 270,
                RenderingBias = RenderingBias.Performance
            };
        }
        else
        {
            chrome.Effect = null;
        }
    }

    private void PassMouseWheelToParent(object sender, MouseWheelEventArgs e)
    {
        if (e.Handled)
        {
            return;
        }

        e.Handled = true;
        GestureBindingPageScrollViewer.ScrollToVerticalOffset(
            GestureBindingPageScrollViewer.VerticalOffset - e.Delta);
    }

    private static bool CanDragFrom(DependencyObject? source)
    {
        while (source is not null)
        {
            if (source is WpfButtonBase
                or WpfTextBoxBase
                or WpfSelector
                or WpfScrollBar
                or TabItem
                or ComboBoxItem
                or ListBoxItem
                or Expander
                or ScrollViewer
                or Hyperlink
                or GesturePatternView)
            {
                return false;
            }

            source = VisualTreeHelper.GetParent(source);
        }

        return true;
    }

    private static T? FindAncestor<T>(DependencyObject? source)
        where T : DependencyObject
    {
        while (source is not null)
        {
            if (source is T target)
            {
                return target;
            }

            source = VisualTreeHelper.GetParent(source);
        }

        return null;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
}
