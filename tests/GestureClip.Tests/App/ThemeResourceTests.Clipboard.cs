using Xunit;
using GestureClip.Tests.TestDoubles;

namespace GestureClip.Tests.App;

public sealed partial class ThemeResourceTests
{

    [Fact]
    public void ClipboardOverlayWindow_uses_glass_panel_styling()
    {
        var path = FindRepositoryFile("src", "GestureClip.App", "ClipboardOverlayWindow.xaml");
        var xaml = File.ReadAllText(path);

        Assert.Contains("Width=\"860\"", xaml);
        Assert.Contains("Height=\"760\"", xaml);
        Assert.Contains("AllowsTransparency=\"True\"", xaml);
        Assert.Contains("CornerRadius=\"18\"", xaml);
        Assert.Contains("Background=\"{DynamicResource BrushOverlayPanel}\"", xaml);
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
        var xaml = File.ReadAllText(xamlPath);
        var source = ReadClipboardOverlaySources();

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
    public void ClipboardOverlay_uses_large_image_preview_cards_without_base64_text_for_images()
    {
        var path = FindRepositoryFile("src", "GestureClip.App", "ClipboardOverlayWindow.xaml");
        var xaml = File.ReadAllText(path);
        var codeBehind = ReadClipboardOverlaySources();

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
}
