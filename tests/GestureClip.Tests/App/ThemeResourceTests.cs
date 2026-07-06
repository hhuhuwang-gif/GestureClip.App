using Xunit;

namespace GestureClip.Tests.App;

public sealed class ThemeResourceTests
{
    [Fact]
    public void App_merges_glass_theme_resource_dictionaries()
    {
        var appPath = FindRepositoryFile("src", "GestureClip.App", "App.xaml");
        var appXaml = File.ReadAllText(appPath);

        Assert.Contains("Themes/Colors.xaml", appXaml);
        Assert.Contains("Themes/Brushes.xaml", appXaml);
        Assert.Contains("Themes/Controls.xaml", appXaml);
        Assert.Contains("Themes/GlassStyles.xaml", appXaml);
    }

    [Fact]
    public void App_supports_smoke_exit_after_startup_for_release_verification()
    {
        var appPath = FindRepositoryFile("src", "GestureClip.App", "App.xaml.cs");
        var source = File.ReadAllText(appPath);

        Assert.Contains("--smoke-exit-after-startup", source);
        Assert.Contains("ExitApplication()", source);
    }

    [Fact]
    public void Theme_resources_define_core_glass_styles()
    {
        var controlsPath = FindRepositoryFile("src", "GestureClip.App", "Themes", "Controls.xaml");
        var glassPath = FindRepositoryFile("src", "GestureClip.App", "Themes", "GlassStyles.xaml");
        var controls = File.ReadAllText(controlsPath);
        var glass = File.ReadAllText(glassPath);

        Assert.Contains("PrimaryButtonStyle", controls);
        Assert.Contains("SecondaryButtonStyle", controls);
        Assert.Contains("DangerButtonStyle", controls);
        Assert.Contains("GlassTextBoxStyle", controls);
        Assert.Contains("GlassComboBoxStyle", controls);
        Assert.Contains("GlassCheckBoxStyle", controls);
        Assert.Contains("GlassListBoxStyle", controls);
        Assert.Contains("GlassTabItemStyle", controls);
        Assert.Contains("GlassCardStyle", glass);
    }

    [Fact]
    public void Theme_uses_readable_soft_macos_inspired_palette()
    {
        var colorsPath = FindRepositoryFile("src", "GestureClip.App", "Themes", "Colors.xaml");
        var colors = File.ReadAllText(colorsPath);

        Assert.Contains("#F6F7FB", colors);
        Assert.Contains("#FFFFFFFF", colors);
        Assert.Contains("#17182733", colors);
        Assert.Contains("#15171D", colors);
        Assert.Contains("#5E6675", colors);
        Assert.Contains("#101011", colors);
        Assert.Contains("#F36D64", colors);
        Assert.Contains("ColorTabSelected", colors);
    }

    [Fact]
    public void ComboBox_theme_defines_readable_dropdown_and_item_states()
    {
        var controlsPath = FindRepositoryFile("src", "GestureClip.App", "Themes", "Controls.xaml");
        var brushesPath = FindRepositoryFile("src", "GestureClip.App", "Themes", "Brushes.xaml");
        var controls = File.ReadAllText(controlsPath);
        var brushes = File.ReadAllText(brushesPath);

        Assert.Contains("GlassComboBoxItemStyle", controls);
        Assert.Contains("PART_Popup", controls);
        Assert.Contains("BrushComboBoxDropDown", brushes);
        Assert.Contains("BrushControlHover", brushes);
        Assert.Contains("BrushControlSelected", brushes);
        Assert.Contains("IsHighlighted", controls);
        Assert.Contains("IsSelected", controls);
    }

    [Fact]
    public void ClipboardOverlayWindow_uses_glass_panel_styling()
    {
        var path = FindRepositoryFile("src", "GestureClip.App", "ClipboardOverlayWindow.xaml");
        var xaml = File.ReadAllText(path);

        Assert.Contains("Width=\"860\"", xaml);
        Assert.Contains("Height=\"760\"", xaml);
        Assert.Contains("AllowsTransparency=\"True\"", xaml);
        Assert.Contains("CornerRadius=\"28\"", xaml);
        Assert.Contains("Background=\"{DynamicResource BrushGlassStrong}\"", xaml);
        Assert.Contains("IsSelected", xaml);
        Assert.Contains("ShortcutNumberConverter", xaml);
        Assert.Contains("VirtualizingPanel.IsVirtualizing=\"True\"", xaml);
        Assert.Contains("VirtualizingPanel.VirtualizationMode=\"Recycling\"", xaml);
        Assert.Contains("ScrollViewer.CanContentScroll=\"True\"", xaml);
        Assert.Contains("ScrollViewer.IsDeferredScrollingEnabled=\"True\"", xaml);
        Assert.Contains("<VirtualizingStackPanel />", xaml);
        Assert.Contains("Binding ThumbnailContent, IsAsync=True", xaml);
        Assert.Contains("Binding SelectedItem.ThumbnailContent, IsAsync=True", xaml);
    }


    [Fact]
    public void ClipboardOverlayWindow_uses_scroll_to_load_more_without_covering_button()
    {
        var xamlPath = FindRepositoryFile("src", "GestureClip.App", "ClipboardOverlayWindow.xaml");
        var sourcePath = FindRepositoryFile("src", "GestureClip.App", "ClipboardOverlayWindow.xaml.cs");
        var xaml = File.ReadAllText(xamlPath);
        var source = File.ReadAllText(sourcePath);

        Assert.Contains("ScrollChanged=\"HistoryList_ScrollChanged\"", xaml);
        Assert.DoesNotContain("加载更多", xaml);
        Assert.Contains("LoadMoreAsync", source);
    }

    [Fact]
    public void ClipboardOverlayWindow_exposes_detail_action_buttons()
    {
        var path = FindRepositoryFile("src", "GestureClip.App", "ClipboardOverlayWindow.xaml");
        var xaml = File.ReadAllText(path);

        Assert.Contains("x:Name=\"DetailCopyButton\"", xaml);
        Assert.Contains("x:Name=\"DetailPasteButton\"", xaml);
        Assert.Contains("x:Name=\"DetailPinButton\"", xaml);
        Assert.Contains("x:Name=\"DetailFavoriteButton\"", xaml);
        Assert.Contains("x:Name=\"DetailDeleteButton\"", xaml);
    }

    [Fact]
    public void ClipboardOverlayWindow_exposes_per_item_quick_action_buttons()
    {
        var xamlPath = FindRepositoryFile("src", "GestureClip.App", "ClipboardOverlayWindow.xaml");
        var sourcePath = FindRepositoryFile("src", "GestureClip.App", "ClipboardOverlayWindow.xaml.cs");
        var xaml = File.ReadAllText(xamlPath);
        var source = File.ReadAllText(sourcePath);

        Assert.Contains("x:Name=\"ItemQuickCopyButton\"", xaml);
        Assert.Contains("x:Name=\"ItemQuickPasteButton\"", xaml);
        Assert.Contains("x:Name=\"ItemQuickPinButton\"", xaml);
        Assert.Contains("x:Name=\"ItemQuickDeleteButton\"", xaml);
        Assert.Contains("QuickCopyItemButton_Click", source);
        Assert.Contains("QuickPasteItemButton_Click", source);
        Assert.Contains("QuickPinItemButton_Click", source);
        Assert.Contains("QuickDeleteItemButton_Click", source);
        Assert.Contains("SelectSingleItem", source);
    }

    [Fact]
    public void ClipboardOverlayWindow_supports_search_friendly_keyboard_shortcuts()
    {
        var path = FindRepositoryFile("src", "GestureClip.App", "ClipboardOverlayWindow.xaml.cs");
        var source = File.ReadAllText(path);

        Assert.Contains("ClearSearchAsync", source);
        Assert.Contains("Key.F", source);
        Assert.Contains("FocusSearchBox", source);
    }

    [Fact]
    public void ClipboardOverlayWindow_exposes_clear_search_button()
    {
        var xamlPath = FindRepositoryFile("src", "GestureClip.App", "ClipboardOverlayWindow.xaml");
        var sourcePath = FindRepositoryFile("src", "GestureClip.App", "ClipboardOverlayWindow.xaml.cs");
        var xaml = File.ReadAllText(xamlPath);
        var source = File.ReadAllText(sourcePath);

        Assert.Contains("x:Name=\"ClearSearchButton\"", xaml);
        Assert.Contains("Click=\"ClearSearchButton_Click\"", xaml);
        Assert.Contains("ClearSearchButton_Click", source);
        Assert.Contains("ClearSearchAsync", source);
    }

    [Fact]
    public void ClipboardOverlayWindow_supports_fast_office_keyboard_shortcuts()
    {
        var path = FindRepositoryFile("src", "GestureClip.App", "ClipboardOverlayWindow.xaml.cs");
        var source = File.ReadAllText(path);

        Assert.Contains("Key.S", source);
        Assert.Contains("ToggleSelectedFavoriteAsync", source);
        Assert.Contains("Key.P", source);
        Assert.Contains("ToggleSelectedPinnedAsync", source);
        Assert.Contains("SelectFilterByShortcut", source);
    }

    [Fact]
    public void ClipboardOverlayWindow_shows_office_shortcut_hints()
    {
        var path = FindRepositoryFile("src", "GestureClip.App", "ClipboardOverlayWindow.xaml");
        var xaml = File.ReadAllText(path);

        Assert.Contains("Ctrl + 1-5", xaml);
        Assert.Contains("Ctrl + P", xaml);
        Assert.Contains("Ctrl + S", xaml);
        Assert.Contains("Delete", xaml);
    }

    [Fact]
    public void ClipboardOverlayWindow_shows_selection_summary()
    {
        var xamlPath = FindRepositoryFile("src", "GestureClip.App", "ClipboardOverlayWindow.xaml");
        var sourcePath = FindRepositoryFile("src", "GestureClip.App", "ClipboardOverlayWindow.xaml.cs");
        var xaml = File.ReadAllText(xamlPath);
        var source = File.ReadAllText(sourcePath);

        Assert.Contains("Text=\"{Binding SummaryText}\"", xaml);
        Assert.Contains("SelectionChanged=\"HistoryList_SelectionChanged\"", xaml);
        Assert.Contains("UpdateSelectedCount", source);
    }

    [Fact]
    public void MouseGestureService_does_not_reinsert_start_point_when_trimming_overlay_points()
    {
        var path = FindRepositoryFile("src", "GestureClip.Features", "Gestures", "MouseGestureService.cs");
        var source = File.ReadAllText(path);

        Assert.Contains("RemoveRange(0, overflow)", source);
        Assert.DoesNotContain(".Insert(0", source);
        Assert.DoesNotContain("_startPoint.Value", source);
    }

    [Fact]
    public void SettingsWindow_keeps_readonly_textbox_bindings_one_way()
    {
        var path = FindRepositoryFile("src", "GestureClip.App", "SettingsWindow.xaml");
        var xaml = File.ReadAllText(path);

        Assert.Contains("Text=\"{Binding DatabasePath, Mode=OneWay}\"", xaml);
        Assert.Contains("Text=\"{Binding LogDirectory, Mode=OneWay}\"", xaml);
        Assert.Contains("Text=\"{Binding DiagnosticsText, Mode=OneWay}\"", xaml);
        Assert.Contains("导出诊断包", xaml);
        Assert.Contains("ExportDiagnosticsCommand", xaml);
        Assert.DoesNotContain("Text=\"{Binding DatabasePath}\"", xaml);
        Assert.DoesNotContain("Text=\"{Binding LogDirectory}\"", xaml);
        Assert.DoesNotContain("Text=\"{Binding DiagnosticsText}\"", xaml);
    }

    [Fact]
    public void SettingsWindow_uses_custom_rounded_shell_and_gesture_customization_controls()
    {
        var path = FindRepositoryFile("src", "GestureClip.App", "SettingsWindow.xaml");
        var xaml = File.ReadAllText(path);

        Assert.Contains("WindowStyle=\"None\"", xaml);
        Assert.Contains("AllowsTransparency=\"True\"", xaml);
        Assert.Contains("CornerRadius=\"28\"", xaml);
        Assert.Contains("Background=\"#CCFFFFFF\"", xaml);
        Assert.Contains("BorderBrush=\"{DynamicResource BrushBorder}\"", xaml);
        Assert.Contains("搜索设置稍后开放", xaml);
        Assert.Contains("GestureStrokeColorOptions", xaml);
        Assert.Contains("NewGesturePattern", xaml);
        Assert.Contains("AddCustomGestureBindingCommand", xaml);
        Assert.Contains("SettingRowStyle", xaml);
        Assert.Contains("OpenClipboardHotkeyText", xaml);
        Assert.DoesNotContain("TabItem Header=\"数据与清理\"", xaml);
    }

    [Fact]
    public void SettingsWindow_uses_larger_window_controls_and_custom_scrollbars()
    {
        var settingsPath = FindRepositoryFile("src", "GestureClip.App", "SettingsWindow.xaml");
        var workstationPath = FindRepositoryFile("src", "GestureClip.App", "WorkstationDashboardWindow.xaml");
        var controlsPath = FindRepositoryFile("src", "GestureClip.App", "Themes", "Controls.xaml");
        var settings = File.ReadAllText(settingsPath);
        var workstation = File.ReadAllText(workstationPath);
        var controls = File.ReadAllText(controlsPath);

        Assert.Contains("Width=\"42\"", settings);
        Assert.Contains("Height=\"34\"", settings);
        Assert.Contains("MinWidth=\"0\"", settings);
        Assert.Contains("Focusable=\"False\"", settings);
        Assert.Contains("FocusVisualStyle=\"{x:Null}\"", settings);
        Assert.Contains("Width=\"42\"", workstation);
        Assert.Contains("Height=\"34\"", workstation);
        Assert.Contains("FocusVisualStyle=\"{x:Null}\"", workstation);
        Assert.Contains("GlassScrollBarStyle", controls);
        Assert.Contains("ScrollBarThumb", controls);
        Assert.Contains("CornerRadius=\"6\"", controls);
        Assert.Contains("Opacity=\"0.38\"", controls);
    }

    [Fact]
    public void ClipboardOverlay_uses_large_image_preview_cards_without_base64_text_for_images()
    {
        var path = FindRepositoryFile("src", "GestureClip.App", "ClipboardOverlayWindow.xaml");
        var codeBehindPath = FindRepositoryFile("src", "GestureClip.App", "ClipboardOverlayWindow.xaml.cs");
        var xaml = File.ReadAllText(path);
        var codeBehind = File.ReadAllText(codeBehindPath);

        Assert.Contains("图片缩略图", xaml);
        Assert.Contains("双击复制并关闭，可放回系统剪贴板", xaml);
        Assert.Contains("CopySelectedAsync(GetSelectedItems())", codeBehind);
        Assert.Contains("SelectedImagePreviewPanel", xaml);
        Assert.Contains("ImageItemPreviewTextBlock", xaml);
        Assert.Contains("Height=\"260\"", xaml);
        Assert.Contains("IsSelectedImage", xaml);
        Assert.Contains("Image Source=\"{Binding ThumbnailContent, IsAsync=True", xaml);
        Assert.Contains("Image Source=\"{Binding SelectedItem.ThumbnailContent, IsAsync=True", xaml);
        Assert.DoesNotContain("Image Source=\"{Binding TextContent", xaml);
        Assert.DoesNotContain("Image Source=\"{Binding SelectedItem.TextContent", xaml);
        Assert.DoesNotContain("_viewModel.SelectedItem?.IsImage == true", codeBehind);
        Assert.Contains("CopySelectedAsync(GetSelectedItems())", codeBehind);
    }

    [Fact]
    public void GestureBindingEditor_uses_single_scroll_flow_and_large_pattern_preview()
    {
        var path = FindRepositoryFile("src", "GestureClip.App", "SettingsWindow.xaml");
        var xaml = File.ReadAllText(path);

        Assert.Contains("GestureDesignerPanel", xaml);
        Assert.Contains("GestureBindingListPanel", xaml);
        Assert.Contains("GestureBindingDetailPanel", xaml);
        Assert.Contains("GestureBindingPageScrollViewer", xaml);
        Assert.Contains("Click=\"ScrollToCustomGestureDesigner_Click\"", xaml);
        Assert.Contains("MinHeight=\"220\"", xaml);
        Assert.Contains("删除这个手势", xaml);
        Assert.Contains("动作编辑器会跟着页面一起滑动，不会固定挡住内容。", xaml);
        Assert.Contains("FocusVisualStyle=\"{x:Null}\"", xaml);

        var detailStart = xaml.IndexOf("x:Name=\"GestureBindingDetailPanel\"", StringComparison.Ordinal);
        Assert.True(detailStart >= 0);
        var detailEnd = xaml.IndexOf("</Border>", detailStart, StringComparison.Ordinal);
        var detailHeader = xaml[detailStart..detailEnd];
        Assert.DoesNotContain("Grid.Column=\"1\"", detailHeader);
        Assert.DoesNotContain("Margin=\"14,0,0,0\"", detailHeader);
        Assert.Contains("Margin=\"0,14,0,0\"", detailHeader);
    }

    [Fact]
    public void SettingsWindow_keeps_gesture_page_simple_with_advanced_settings_expanded_and_left_button_off_by_default()
    {
        var path = FindRepositoryFile("src", "GestureClip.App", "SettingsWindow.xaml");
        var xaml = File.ReadAllText(path);

        Assert.Contains("按住右键，往一个方向划一下，松开右键就会执行动作。", xaml);
        Assert.Contains("Header=\"高级设置\"", xaml);
        Assert.Contains("IsExpanded=\"True\"", xaml);
        Assert.DoesNotContain("启用左键拖动画手势", xaml);
        Assert.Contains("左键点击可以作为执行动作，但不是手势触发键", xaml);
        Assert.DoesNotContain("左边缘 + 鼠标左键", xaml);
        Assert.Contains("划多远才算手势", xaml);
        Assert.Contains("防误触间隔", xaml);
        Assert.DoesNotContain("停留 ms", xaml);
        Assert.DoesNotContain("冷却 ms", xaml);
    }

    [Fact]
    public void WorkstationDashboardWindow_uses_glass_cards_and_plain_language_metrics()
    {
        var path = FindRepositoryFile("src", "GestureClip.App", "WorkstationDashboardWindow.xaml");
        var xaml = File.ReadAllText(path);

        Assert.Contains("工位小熊", xaml);
        Assert.Contains("OffWorkCountdownText", xaml);
        Assert.Contains("TodayEarnedText", xaml);
        Assert.Contains("TodayFishingValueText", xaml);
        Assert.Contains("ActionStatsText", xaml);
        Assert.Contains("GlassCardStyle", xaml);
    }

    [Fact]
    public void WorkstationDashboard_has_tray_and_lifecycle_entry()
    {
        var lifecyclePath = FindRepositoryFile("src", "GestureClip.App", "Services", "AppLifecycleService.cs");
        var trayPath = FindRepositoryFile("src", "GestureClip.App", "Services", "TrayIconService.cs");
        var lifecycle = File.ReadAllText(lifecyclePath);
        var tray = File.ReadAllText(trayPath);

        Assert.Contains("ShowWorkstationDashboardWindow", lifecycle);
        Assert.Contains("WorkstationDashboardWindow", lifecycle);
        Assert.Contains("工位小熊", tray);
        Assert.Contains("ShowWorkstationDashboardWindow", tray);
    }

    [Fact]
    public void TrayIconService_respects_workstation_enabled_setting_for_dashboard_entry()
    {
        var path = FindRepositoryFile("src", "GestureClip.App", "Services", "TrayIconService.cs");
        var source = File.ReadAllText(path);

        Assert.Contains("ISettingsService", source);
        Assert.Contains("SettingKeys.WorkstationEnabled", source);
        Assert.Contains("ShowWorkstationDashboardWindow", source);
    }

    [Fact]
    public void SettingsWindow_exposes_workstation_dashboard_settings()
    {
        var path = FindRepositoryFile("src", "GestureClip.App", "SettingsWindow.xaml");
        var xaml = File.ReadAllText(path);

        Assert.Contains("TabItem Header=\"小熊\"", xaml);
        Assert.Contains("WorkstationEnabled", xaml);
        Assert.Contains("WorkstationMonthlySalary", xaml);
        Assert.Contains("WorkstationWorkStartTime", xaml);
        Assert.Contains("WorkstationWorkEndTime", xaml);
        Assert.Contains("WorkstationWorkdays", xaml);
        Assert.Contains("WorkstationPayday", xaml);
        Assert.Contains("WorkstationShowFishingValue", xaml);
        Assert.Contains("WorkstationDailyReportEnabled", xaml);
        Assert.True(
            xaml.IndexOf("TabItem Header=\"小熊\"", StringComparison.Ordinal) <
            xaml.IndexOf("WorkstationWorkdays", StringComparison.Ordinal));
    }

    [Fact]
    public void MainWindow_exposes_workstation_dashboard_button()
    {
        var xamlPath = FindRepositoryFile("src", "GestureClip.App", "MainWindow.xaml");
        var sourcePath = FindRepositoryFile("src", "GestureClip.App", "MainWindow.xaml.cs");
        var xaml = File.ReadAllText(xamlPath);
        var source = File.ReadAllText(sourcePath);

        Assert.Contains("工位小熊", xaml);
        Assert.Contains("OpenWorkstationButton_Click", xaml);
        Assert.Contains("ShowWorkstationDashboardWindow", source);
    }

    [Fact]
    public void App_exposes_one_click_update_entry_to_latest_release()
    {
        var lifecyclePath = FindRepositoryFile("src", "GestureClip.App", "Services", "AppLifecycleService.cs");
        var trayPath = FindRepositoryFile("src", "GestureClip.App", "Services", "TrayIconService.cs");
        var settingsXamlPath = FindRepositoryFile("src", "GestureClip.App", "SettingsWindow.xaml");
        var settingsSourcePath = FindRepositoryFile("src", "GestureClip.App", "SettingsWindow.xaml.cs");
        var lifecycle = File.ReadAllText(lifecyclePath);
        var tray = File.ReadAllText(trayPath);
        var settingsXaml = File.ReadAllText(settingsXamlPath);
        var settingsSource = File.ReadAllText(settingsSourcePath);

        Assert.Contains("CheckForUpdatesAsync", lifecycle);
        Assert.Contains("StartCoverUpdateAsync", lifecycle);
        Assert.Contains("IUpdateCheckService", lifecycle);
        Assert.Contains("IUpdateInstallerService", lifecycle);
        Assert.Contains("检查更新", tray);
        Assert.Contains("一键覆盖更新", tray);
        Assert.Contains("检查更新", settingsXaml);
        Assert.Contains("一键覆盖更新", settingsXaml);
        Assert.Contains("GitHub Latest Release", settingsXaml);
        Assert.Contains("CheckUpdateButton_Click", settingsXaml);
        Assert.Contains("UpdateButton_Click", settingsXaml);
        Assert.Contains("CheckForUpdatesAsync", settingsSource);
        Assert.Contains("StartCoverUpdateAsync", settingsSource);
    }

    private static string FindRepositoryFile(params string[] segments)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(new[] { directory.FullName }.Concat(segments).ToArray());
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("Could not locate repository file.", Path.Combine(segments));
    }
    [Fact]
    public void App_icon_click_and_second_launch_toggle_settings_window()
    {
        var trayPath = FindRepositoryFile("src", "GestureClip.App", "Services", "TrayIconService.cs");
        var lifecyclePath = FindRepositoryFile("src", "GestureClip.App", "Services", "AppLifecycleService.cs");
        var appPath = FindRepositoryFile("src", "GestureClip.App", "App.xaml.cs");
        var singlePath = FindRepositoryFile("src", "GestureClip.App", "Services", "SingleInstanceService.cs");

        var tray = File.ReadAllText(trayPath);
        var lifecycle = File.ReadAllText(lifecyclePath);
        var app = File.ReadAllText(appPath);
        var single = File.ReadAllText(singlePath);

        Assert.Contains("ToggleSettingsWindow", lifecycle);
        Assert.Contains("_settingsWindow.Hide()", lifecycle);
        Assert.Contains("ToggleSettingsWindow", tray);
        Assert.Contains("MouseClick", tray);
        Assert.Contains("ActivationRequested", app);
        Assert.Contains("SignalExistingInstance", single);
        var settingsWindowSource = File.ReadAllText(FindRepositoryFile("src", "GestureClip.App", "SettingsWindow.xaml.cs"));
        Assert.Contains("EnableTaskbarMinimizeBehavior", settingsWindowSource);
        Assert.Contains("WsMinimizebox", settingsWindowSource);
    }

    [Fact]
    public void Custom_gesture_entry_is_prominent_and_not_hidden_in_collapsed_expander()
    {
        var path = FindRepositoryFile("src", "GestureClip.App", "SettingsWindow.xaml");
        var xaml = File.ReadAllText(path);

        Assert.Contains("+ 新建自定义手势", xaml);
        Assert.Contains("创建你的专属手势", xaml);
        Assert.Contains("先画手势，再绑定动作", xaml);
        Assert.Contains("办公高频", xaml);
        Assert.Contains("浏览高频", xaml);
        Assert.Contains("实用动作", xaml);
        Assert.DoesNotContain("<Expander Header=\"添加自定义手势\" IsExpanded=\"False\"", xaml);
    }

    [Fact]
    public void Workstation_settings_show_templates_and_live_preview()
    {
        var path = FindRepositoryFile("src", "GestureClip.App", "SettingsWindow.xaml");
        var xaml = File.ReadAllText(path);

        Assert.Contains("工作制模板", xaml);
        Assert.Contains("实时预览", xaml);
        Assert.Contains("WorkstationTemplateOptions", xaml);
        Assert.Contains("ApplyWorkstationTemplateCommand", xaml);
        Assert.Contains("WorkstationPreviewTodayEarnedText", xaml);
        Assert.Contains("WorkstationPreviewOffWorkText", xaml);
        Assert.Contains("WorkstationPreviewPaydayText", xaml);
    }

    [Fact]
    public void Gesture_editor_explains_left_click_as_confirm_not_action()
    {
        var path = FindRepositoryFile("src", "GestureClip.App", "SettingsWindow.xaml");
        var xaml = File.ReadAllText(path);

        Assert.Contains("R+L 组合手势", xaml);
        Assert.Contains("CommandParameter=\"R+L|PasteAndEnter\"", xaml);
        Assert.DoesNotContain("CommandParameter=\"LeftMouseClick\"", xaml);
        Assert.DoesNotContain("CommandParameter=\"RightMouseClick\"", xaml);
        Assert.DoesNotContain("GestureLeftButtonEnabled, Mode=TwoWay", xaml);
    }

    [Fact]
    public void Custom_gesture_action_form_uses_roomy_vertical_layout()
    {
        var path = FindRepositoryFile("src", "GestureClip.App", "SettingsWindow.xaml");
        var xaml = File.ReadAllText(path);

        Assert.Contains("x:Name=\"NewGestureFormPanel\"", xaml);
        Assert.Contains("Text=\"手势码\"", xaml);
        Assert.Contains("Text=\"执行动作\"", xaml);
        Assert.Contains("Header=\"快捷动作\"", xaml);
        Assert.Contains("Style=\"{StaticResource PrimaryButtonStyle}\"", xaml);
        Assert.Contains("Height=\"40\"", xaml);
        Assert.DoesNotContain("<TextBox Width=\"120\" Text=\"{Binding NewGesturePattern", xaml);
        Assert.DoesNotContain("<ComboBox Width=\"220\" Margin=\"10,0,0,0\"", xaml);
    }

    [Fact]
    public void Gesture_trigger_section_exposes_direct_switch_bindings()
    {
        var path = FindRepositoryFile("src", "GestureClip.App", "SettingsWindow.xaml");
        var xaml = File.ReadAllText(path);

        Assert.Contains("GestureRightButtonEnabled", xaml);
        Assert.Contains("GestureMiddleButtonEnabled", xaml);
        Assert.Contains("GestureXButton1Enabled", xaml);
        Assert.Contains("GestureXButton2Enabled", xaml);
        Assert.Contains("EdgeTriggerEnabled", xaml);
        Assert.Contains("当前启用", xaml);
    }

    [Fact]
    public void Workstation_settings_group_display_options_and_copywriting_style()
    {
        var path = FindRepositoryFile("src", "GestureClip.App", "SettingsWindow.xaml");
        var xaml = File.ReadAllText(path);

        Assert.Contains("基础显示", xaml);
        Assert.Contains("趣味显示", xaml);
        Assert.Contains("统计显示", xaml);
        Assert.Contains("文案风格", xaml);
        Assert.Contains("WorkstationCopywritingStyle", xaml);
        Assert.Contains("正常模式", xaml);
        Assert.Contains("打工人模式", xaml);
        Assert.Contains("抽象模式", xaml);
    }

    [Fact]
    public void Workstation_settings_exposes_overwork_reminder_controls()
    {
        var path = FindRepositoryFile("src", "GestureClip.App", "SettingsWindow.xaml");
        var xaml = File.ReadAllText(path);

        Assert.Contains("猝死提醒", xaml);
        Assert.Contains("开启过劳提醒", xaml);
        Assert.Contains("WorkstationEnableOverworkReminder", xaml);
        Assert.Contains("WorkstationOverworkReminderIntervalMinutes", xaml);
        Assert.Contains("WorkstationOverworkHighRiskAfterHours", xaml);
        Assert.Contains("WorkstationEnableHudTimeColor", xaml);
        Assert.Contains("WorkstationEnableStrongOverworkWarning", xaml);
        Assert.Contains("WorkstationOverworkReminderCanSnooze", xaml);
        Assert.Contains("WorkstationOverworkSnoozeMinutes", xaml);
        Assert.Contains("OverworkPreviewStageText", xaml);
        Assert.Contains("OverworkPreviewHudColorText", xaml);
        Assert.Contains("OverworkPreviewNextReminderText", xaml);
        Assert.Contains("OverworkPreviewWorkedText", xaml);
    }

    [Fact]
    public void GestureOverlayWindow_binds_hud_time_theme_brushes()
    {
        var path = FindRepositoryFile("src", "GestureClip.App", "GestureOverlayWindow.xaml");
        var xaml = File.ReadAllText(path);

        Assert.Contains("Background=\"{Binding HudBackgroundBrush}\"", xaml);
        Assert.Contains("Foreground=\"{Binding HudAccentBrush}\"", xaml);
        Assert.Contains("Foreground=\"{Binding HudAccentBrush}\"", xaml);
        Assert.Contains("HudAccentBrush", xaml);
    }

    [Fact]
    public void GestureOverlayWindow_uses_readable_hud_text_hierarchy()
    {
        var path = FindRepositoryFile("src", "GestureClip.App", "GestureOverlayWindow.xaml");
        var xaml = File.ReadAllText(path);

        Assert.Contains("Background=\"#E6111724\"", xaml);
        Assert.Contains("手势码:", xaml);
        Assert.Contains("FontSize=\"34\"", xaml);
        Assert.Contains("FontSize=\"28\"", xaml);
        Assert.Contains("FontSize=\"14\"", xaml);
        Assert.DoesNotContain("Pattern: ", xaml);
    }

    [Fact]
    public void App_starts_and_stops_overwork_reminder_service()
    {
        var appPath = FindRepositoryFile("src", "GestureClip.App", "App.xaml.cs");
        var lifecyclePath = FindRepositoryFile("src", "GestureClip.App", "Services", "AppLifecycleService.cs");
        var app = File.ReadAllText(appPath);
        var lifecycle = File.ReadAllText(lifecyclePath);

        Assert.Contains("IOverworkReminderService", app);
        Assert.Contains("StartAsync(CancellationToken.None)", app);
        Assert.Contains("overwork reminder service", lifecycle);
        Assert.Contains("IOverworkReminderService", lifecycle);
        Assert.Contains("StopAsync(CancellationToken.None)", lifecycle);
    }

    [Fact]
    public void App_registers_overwork_reminder_toast_window()
    {
        var diPath = FindRepositoryFile("src", "GestureClip.App", "DependencyInjection", "AppServiceCollectionExtensions.cs");
        var di = File.ReadAllText(diPath);

        Assert.Contains("IOverworkReminderToastService", di);
        Assert.Contains("OverworkReminderToastService", di);
        Assert.Contains("OverworkReminderToastWindow", di);
    }

    [Fact]
    public void SettingsWindow_hides_unused_edge_mouse_button_section()
    {
        var path = FindRepositoryFile("src", "GestureClip.App", "SettingsWindow.xaml");
        var xaml = File.ReadAllText(path);

        Assert.DoesNotContain("边缘 + 鼠标按钮", xaml);
        Assert.DoesNotContain("左边缘 + 鼠标中键", xaml);
        Assert.DoesNotContain("左边缘 + 鼠标侧键 1", xaml);
        Assert.DoesNotContain("左边缘 + 鼠标侧键 2", xaml);
        Assert.DoesNotContain("右上角 + 滚轮", xaml);
    }
}
