using Xunit;
using GestureClip.Tests.TestDoubles;

namespace GestureClip.Tests.App;

public sealed partial class ThemeResourceTests
{

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
    public void GestureBindingEditor_uses_single_scroll_flow_and_large_pattern_preview()
    {
        var path = FindRepositoryFile("src", "GestureClip.App", "SettingsWindow.xaml");
        var xaml = File.ReadAllText(path);

        Assert.Contains("GestureDesignerPanel", xaml);
        Assert.Contains("GestureBindingListPanel", xaml);
        Assert.Contains("GestureBindingDetailPanel", xaml);
        Assert.Contains("GestureBindingPageScrollViewer", xaml);
        Assert.Contains("WindowBackground_MouseLeftButtonDown", xaml);
        Assert.Contains("Click=\"ScrollToCustomGestureDesigner_Click\"", xaml);
        Assert.Contains("MinHeight=\"210\"", xaml);
        Assert.Contains("删除这个手势绑定", xaml);
        Assert.Contains("点「更换动作」会选中该手势", xaml);
        Assert.Contains("常用手势一览", xaml);
        Assert.Contains("ChangeGestureAction_Click", xaml);
        Assert.Contains("Tag=\"{Binding Pattern}\"", xaml);
        Assert.Contains("<UniformGrid Columns=\"2\" />", xaml);
        Assert.Contains("MinHeight=\"180\"", xaml);
        Assert.Contains("Focusable=\"False\"", xaml);
        Assert.Contains("TrajectoryGlyphs", xaml);
        Assert.Contains("InstructionText", xaml);
        Assert.DoesNotContain("<ListBox ItemsSource=\"{Binding PrimaryGestureBindingCards}\"", xaml);
        Assert.DoesNotContain("<ListBox ItemsSource=\"{Binding AdvancedGestureBindingCards}\"", xaml);
        Assert.DoesNotContain("SelectedItem=\"{Binding SelectedGestureBindingCard, Mode=TwoWay}\"", xaml);


        var detailStart = xaml.IndexOf("x:Name=\"GestureBindingDetailPanel\"", StringComparison.Ordinal);
        Assert.True(detailStart >= 0);
        var detailEnd = xaml.IndexOf("</Border>", detailStart, StringComparison.Ordinal);
        var detailHeader = xaml[detailStart..detailEnd];
        Assert.DoesNotContain("Grid.Column=\"1\"", detailHeader);
        Assert.DoesNotContain("Margin=\"14,0,0,0\"", detailHeader);
        Assert.Contains("Margin=\"0,16,0,0\"", detailHeader);
    }

    [Fact]
    public void Gesture_binding_cards_have_clear_selection_delete_and_empty_state_contract()
    {
        var path = FindRepositoryFile("src", "GestureClip.App", "SettingsWindow.xaml");
        var xaml = File.ReadAllText(path);

        Assert.Contains("SelectedBadgeText", xaml);
        Assert.Contains("当前选中", File.ReadAllText(FindRepositoryFile("src", "GestureClip.App", "ViewModels", "GestureBindingCardViewModel.cs")));
        Assert.Contains("SelectedAccent", xaml);
        Assert.Contains("Binding=\"{Binding IsSelected}\"", xaml);
        Assert.Contains("Content=\"更换动作\"", xaml);
        Assert.Contains("Content=\"删除\"", xaml);
        Assert.Contains("ToolTip=\"删除这个手势绑定\"", xaml);
        Assert.Contains("Command=\"{Binding DeleteCommand}\"", xaml);
        Assert.Contains("当前绑定动作", xaml);
        Assert.Contains("按住右键画这个手势后", File.ReadAllText(FindRepositoryFile("src", "GestureClip.App", "ViewModels", "GestureBindingCardViewModel.cs")));
        Assert.Contains("CustomGestureEmptyStateText", xaml);
        Assert.Contains("GestureBindingEmptyStateText", xaml);
        Assert.Contains("还没有自定义手势", ReadAllSettingsViewModelSources());
        if (xaml.Contains("TargetType=\"{x:Type ListBoxItem}\"", StringComparison.Ordinal))
        {
            Assert.Contains("BasedOn=\"{StaticResource {x:Type ListBoxItem}}\"", xaml);
        }
    }

    [Fact]
    public void Gesture_binding_page_exposes_new_user_recommendation_contract()
    {
        var path = FindRepositoryFile("src", "GestureClip.App", "SettingsWindow.xaml");
        var xaml = File.ReadAllText(path);
        var viewModelSource = ReadAllSettingsViewModelSources();

        Assert.Contains("RecommendedGesturePanel", xaml);
        Assert.Contains("推荐给新手的 3 个手势", xaml);
        Assert.Contains("先用这 3 个就够了", xaml);
        Assert.Contains("一键添加推荐手势", xaml);
        Assert.Contains("已有的自定义手势不会被删除", xaml);
        Assert.Contains("ItemsSource=\"{Binding RecommendedGestureBindings}\"", xaml);
        Assert.Contains("Command=\"{Binding ApplyRecommendedGestureBindingsCommand}\"", xaml);
        Assert.Contains("RecommendedGestureStatusText", xaml);
        Assert.Contains("OpenClipboardOverlay", viewModelSource);
        Assert.Contains("BuiltInGestureAction.Paste", viewModelSource);
        Assert.Contains("BuiltInGestureAction.Copy", viewModelSource);
        Assert.Contains("推荐手势已经都在列表里", viewModelSource);
    }

    [Fact]
    public void Custom_gesture_entry_is_prominent_and_not_hidden_in_collapsed_expander()
    {
        var path = FindRepositoryFile("src", "GestureClip.App", "SettingsWindow.xaml");
        var xaml = File.ReadAllText(path);

        Assert.Contains("添加自己的手势", xaml);
        Assert.Contains("添加自己的手势", xaml);
        Assert.Contains("先选择这个手势要执行的动作", xaml);
        Assert.Contains("确认添加到手势列表", xaml);
        Assert.Contains("办公高频", xaml);
        Assert.Contains("浏览高频", xaml);
        Assert.Contains("实用动作", xaml);
        Assert.DoesNotContain("<Expander Header=\"添加自定义手势\" IsExpanded=\"False\"", xaml);
    }

    [Fact]
    public void Gesture_editor_explains_left_click_as_confirm_not_action()
    {
        var path = FindRepositoryFile("src", "GestureClip.App", "SettingsWindow.xaml");
        var xaml = File.ReadAllText(path);

        Assert.Contains("R+L = 按住右键再点左键", xaml);
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
        Assert.Contains("确认添加到手势列表", xaml);
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

        Assert.Contains("Background=\"#F0111724\"", xaml);
        Assert.Contains("手势码", xaml);
        Assert.Contains("Microsoft YaHei UI", xaml);
        Assert.Contains("TextRenderingMode=\"Grayscale\"", xaml);
        Assert.Contains("GestureCountText", xaml);
        Assert.Contains("CopyCountText", xaml);
        Assert.Contains("PasteCountText", xaml);
        Assert.Contains("SavedClicksCountText", xaml);
        Assert.Contains("HudStatChip", xaml);
        Assert.Contains("TodayStatsLine", xaml);
        Assert.DoesNotContain("Pattern: ", xaml);
    }
}
