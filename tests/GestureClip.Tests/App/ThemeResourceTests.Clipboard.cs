using Xunit;
using GestureClip.Tests.TestDoubles;

namespace GestureClip.Tests.App;

public sealed partial class ThemeResourceTests
{

    [Fact]
    public void ClipboardOverlayWindow_uses_compact_opaque_panel_styling()
    {
        var path = FindRepositoryFile("src", "GestureClip.App", "ClipboardOverlayWindow.xaml");
        var xaml = File.ReadAllText(path);

        Assert.Contains("Width=\"700\"", xaml);
        Assert.Contains("Height=\"760\"", xaml);
        Assert.Contains("AllowsTransparency=\"False\"", xaml);
        Assert.Contains("Background=\"{DynamicResource BrushClipboardOverlayPanel}\"", xaml);
        Assert.Contains("IsSelected", xaml);
        Assert.Contains("ShortcutNumberConverter", xaml);
        Assert.Contains("VirtualizingPanel.IsVirtualizing=\"True\"", xaml);
        Assert.Contains("VirtualizingPanel.VirtualizationMode=\"Recycling\"", xaml);
        Assert.Contains("ScrollViewer.CanContentScroll=\"True\"", xaml);
        Assert.Contains("ScrollViewer.IsDeferredScrollingEnabled=\"True\"", xaml);
        Assert.Contains("<VirtualizingStackPanel />", xaml);
        Assert.Contains("Binding ThumbnailContent, IsAsync=True", xaml);
        var accent = System.Xml.Linq.XDocument.Parse(xaml).Descendants().Single(node =>
            (string?)node.Attribute(System.Xml.Linq.XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml")) == "TypeAccentBar");
        Assert.Null(accent.Attribute("Background"));
        Assert.Null(accent.Attribute("Width"));
        Assert.Contains(accent.Descendants(), node =>
            (string?)node.Attribute("Binding") == "{Binding IsSessionCopied}" &&
            node.Elements().Any(setter => (string?)setter.Attribute("Property") == "Background" &&
                (string?)setter.Attribute("Value") == "{DynamicResource BrushSuccess}"));

    }


    [Fact]
    public void ClipboardOverlayWindow_uses_scroll_to_load_more_without_covering_button()
    {
        var xamlPath = FindRepositoryFile("src", "GestureClip.App", "ClipboardOverlayWindow.xaml");
        var xaml = File.ReadAllText(xamlPath);
        var source = ReadClipboardOverlaySources();

        Assert.Contains("ScrollChanged=\"HistoryList_ScrollChanged\"", xaml);
        Assert.DoesNotContain("加载更多", xaml);
        Assert.Contains("LoadMoreAsync", source);
    }

    [Fact]
    public void ClipboardOverlayWindow_keeps_context_actions_without_building_hidden_detail_panel()
    {
        var path = FindRepositoryFile("src", "GestureClip.App", "ClipboardOverlayWindow.xaml");
        var xaml = File.ReadAllText(path);

        Assert.DoesNotContain("DetailCopyButton", xaml);
        Assert.DoesNotContain("SelectedImagePreviewPanel", xaml);
        Assert.Contains("CopySelectedMenuItem_Click", xaml);
        Assert.Contains("PasteSelectedMenuItem_Click", xaml);
        Assert.Contains("TogglePinnedMenuItem_Click", xaml);
        Assert.Contains("ToggleFavoriteMenuItem_Click", xaml);
        Assert.Contains("DeleteSelectedMenuItem_Click", xaml);
    }

    [Fact]
    public void ClipboardOverlayWindow_exposes_per_item_quick_action_buttons()
    {
        var xamlPath = FindRepositoryFile("src", "GestureClip.App", "ClipboardOverlayWindow.xaml");
        var xaml = File.ReadAllText(xamlPath);
        var source = ReadClipboardOverlaySources();

        Assert.Contains("x:Name=\"ItemQuickCopyButton\"", xaml);
        Assert.Contains("x:Name=\"ItemQuickPasteButton\"", xaml);
        Assert.Contains("x:Name=\"ItemQuickPinButton\"", xaml);
        Assert.Contains("x:Name=\"ItemQuickDeleteButton\"", xaml);
        Assert.Contains("QuickCopyItemButton_Click", source);
        Assert.Contains("QuickPasteItemButton_Click", source);
        Assert.Contains("QuickPinItemButton_Click", source);
        Assert.Contains("QuickDeleteItemButton_Click", source);
        Assert.Contains("ConfirmDeleteSelectedItems", source);
        Assert.Contains("删除剪贴板记录", source);
        Assert.Contains("这会从本机剪贴板历史里删除", source);
        Assert.Contains("SelectSingleItem", source);
        Assert.Contains("CopyItemAndHideAsync", source);
        Assert.Contains("OriginalSource", source);
    }

    [Fact]
    public void ClipboardOverlayWindow_supports_search_friendly_keyboard_shortcuts()
    {
        var source = ReadClipboardOverlaySources();

        Assert.Contains("ClearSearchAsync", source);
        Assert.Contains("Key.F", source);
        Assert.Contains("FocusSearchBox", source);
    }

    [Fact]
    public void ClipboardOverlayWindow_exposes_clear_search_button()
    {
        var xamlPath = FindRepositoryFile("src", "GestureClip.App", "ClipboardOverlayWindow.xaml");
        var xaml = File.ReadAllText(xamlPath);
        var source = ReadClipboardOverlaySources();

        Assert.Contains("x:Name=\"ClearSearchButton\"", xaml);
        Assert.Contains("Click=\"ClearSearchButton_Click\"", xaml);
        Assert.Contains("ClearSearchButton_Click", source);
        Assert.Contains("ClearSearchAsync", source);
    }

    [Fact]
    public void ClipboardOverlayWindow_supports_fast_office_keyboard_shortcuts()
    {
        var source = ReadClipboardOverlaySources();

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

        Assert.Contains("ShortcutHintText", xaml);
        Assert.Contains("IconToolbarButtonStyle", xaml);
        Assert.Contains("SegmentedFilter", xaml);
        Assert.Contains("EmptyStatePanelStyle", xaml);
        Assert.Contains("ShortcutHelpText", xaml);
    }

    [Fact]
    public void ClipboardOverlayWindow_shows_selection_summary()
    {
        var xamlPath = FindRepositoryFile("src", "GestureClip.App", "ClipboardOverlayWindow.xaml");
        var xaml = File.ReadAllText(xamlPath);
        var source = ReadClipboardOverlaySources();

        Assert.Contains("Text=\"{Binding SummaryText}\"", xaml);
        Assert.Contains("SelectionChanged=\"HistoryList_SelectionChanged\"", xaml);
        Assert.Contains("UpdateSelectedCount", source);
    }

    [Fact]
    public void ClipboardOverlay_loads_thumbnails_only_for_image_rows()
    {
        var path = FindRepositoryFile("src", "GestureClip.App", "ClipboardOverlayWindow.xaml");
        var xaml = File.ReadAllText(path);
        var codeBehind = ReadClipboardOverlaySources();

        Assert.Contains("ClipboardRowThumbnail", xaml);
        Assert.Contains("图片记录", xaml);
        Assert.Contains("CopySelectedAsync(GetSelectedItems())", codeBehind);
        Assert.DoesNotContain("SelectedImagePreviewPanel", xaml);
        Assert.Contains("ImageItemPreviewTextBlock", xaml);
        Assert.Contains("Image Source=\"{Binding ThumbnailContent, IsAsync=True", xaml);
        Assert.DoesNotContain("Image Source=\"{Binding TextContent", xaml);
        Assert.DoesNotContain("Image Source=\"{Binding SelectedItem.TextContent", xaml);
        Assert.DoesNotContain("_viewModel.SelectedItem?.IsImage == true", codeBehind);
        Assert.Contains("CopySelectedAsync(GetSelectedItems())", codeBehind);
    }
}
