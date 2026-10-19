using GestureClip.App.ViewModels;
using GestureClip.Core.Abstractions;
using GestureClip.Core.Clipboard;
using Xunit;

namespace GestureClip.Tests.App;

public sealed partial class ClipboardOverlayViewModelTests
{
    [Fact]
    public async Task LoadAsync_selects_first_item_and_exposes_text_details()
    {
        var now = new DateTimeOffset(2026, 7, 3, 12, 30, 0, TimeSpan.Zero);
        var item = new ClipboardItem(
            Guid.NewGuid(),
            "text",
            "hello world",
            "hello world",
            "hash",
            "plain",
            "Notepad",
            "notepad.exe",
            false,
            false,
            false,
            3,
            now,
            now,
            now);
        var viewModel = new ClipboardOverlayViewModel(new FakeClipboardService([item]));

        await viewModel.LoadAsync();

        Assert.Same(item, viewModel.SelectedItem);
        Assert.True(viewModel.HasSelectedItem);
        Assert.True(viewModel.IsSelectedText);
        Assert.False(viewModel.IsSelectedImage);
        Assert.Equal("文本", viewModel.SelectedContentTypeText);
        Assert.Equal("notepad.exe", viewModel.SelectedSourceText);
        Assert.Equal("使用 3 次", viewModel.SelectedUseCountText);
    }

    [Fact]
    public void SelectedItem_exposes_image_details()
    {
        var now = DateTimeOffset.UtcNow;
        var pngImage = new ClipboardItem(
            Guid.NewGuid(),
            "image/png",
            "png-base64",
            "图片",
            "hash",
            null,
            "SnippingTool",
            "SnippingTool.exe",
            true,
            false,
            false,
            0,
            now,
            now,
            null);
        var viewModel = new ClipboardOverlayViewModel(new FakeClipboardService([]));

        viewModel.SelectedItem = pngImage;

        Assert.True(viewModel.IsSelectedImage);
        Assert.False(viewModel.IsSelectedText);
        Assert.Equal("图片", viewModel.SelectedContentTypeText);
        Assert.Equal("SnippingTool.exe", viewModel.SelectedSourceText);
        Assert.Equal("还没使用过", viewModel.SelectedUseCountText);
    }

    [Fact]
    public async Task SearchText_debounces_rapid_input_and_searches_last_keyword()
    {
        var service = new FakeClipboardService([]);
        var viewModel = new ClipboardOverlayViewModel(service, TimeSpan.FromMilliseconds(30));

        viewModel.SearchText = "a";
        viewModel.SearchText = "ab";
        viewModel.SearchText = "abc";

        // Letter-only keywords issue one primary search plus one supplemental
        // pinyin-initial pool fetch (empty keyword, larger limit).
        await WaitForAsync(() => service.SearchKeywords.Count == 2);

        Assert.Equal(["abc", ""], service.SearchKeywords);
        Assert.Equal([50, 200], service.SearchLimits);
    }

    [Fact]
    public async Task LoadAsync_uses_default_limit_50()
    {
        var service = new FakeClipboardService([TextItem("first")]);
        var viewModel = new ClipboardOverlayViewModel(service, TimeSpan.Zero);

        await viewModel.LoadAsync();

        Assert.Equal([50], service.SearchLimits);
    }

    [Fact]
    public async Task LoadAsync_reuses_loaded_page_when_history_revision_is_unchanged()
    {
        var service = new FakeClipboardService([TextItem("first")])
        {
            HistoryRevision = 7
        };
        var viewModel = new ClipboardOverlayViewModel(service, TimeSpan.Zero);

        var firstLoad = await viewModel.LoadAsync();
        var secondLoad = await viewModel.LoadAsync();

        Assert.True(firstLoad);
        Assert.False(secondLoad);
        Assert.Single(service.SearchLimits);
    }

    [Fact]
    public async Task LoadAsync_coalesces_concurrent_requests_for_the_same_revision()
    {
        var item = TextItem("first");
        var query = new TaskCompletionSource<IReadOnlyList<ClipboardItem>>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var service = new FakeClipboardService([item])
        {
            HistoryRevision = 7,
            SearchHandler = (_, _, _, _) => query.Task
        };
        var viewModel = new ClipboardOverlayViewModel(service, TimeSpan.Zero);

        var firstLoad = viewModel.LoadAsync();
        var secondLoad = viewModel.LoadAsync();

        Assert.Same(firstLoad, secondLoad);
        await WaitForAsync(() => service.SearchLimits.Count == 1);
        Assert.Single(service.SearchLimits);

        query.SetResult([item]);
        Assert.True(await firstLoad);
        Assert.Single(viewModel.Items);
    }

    [Fact]
    public async Task LoadAsync_refreshes_without_clearing_visible_page_when_history_revision_changes()
    {
        var first = TextItem("first");
        var updated = TextItem("updated");
        var service = new FakeClipboardService([first])
        {
            HistoryRevision = 3
        };
        var viewModel = new ClipboardOverlayViewModel(service, TimeSpan.Zero);
        await viewModel.LoadAsync();

        var refreshResult = new TaskCompletionSource<IReadOnlyList<ClipboardItem>>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        service.HistoryRevision = 4;
        service.SearchHandler = (_, _, _, _) => refreshResult.Task;

        var refreshTask = viewModel.LoadAsync();

        Assert.False(viewModel.IsLoading);
        Assert.Equal("first", viewModel.Items.Single().TextContent);

        refreshResult.SetResult([updated]);
        Assert.True(await refreshTask);
        Assert.Equal("updated", viewModel.Items.Single().TextContent);
        Assert.Equal(2, service.SearchLimits.Count);
    }

    [Fact]
    public async Task LoadAsync_with_1000_text_items_keeps_first_page_to_50()
    {
        var items = Enumerable.Range(0, 1000)
            .Select(index => TextItem($"item {index:0000}"))
            .ToArray();
        var service = new FakeClipboardService(items)
        {
            SearchHandler = (_, limit, offset, _) => Task.FromResult<IReadOnlyList<ClipboardItem>>(items.Take(limit).ToArray())
        };
        var viewModel = new ClipboardOverlayViewModel(service, TimeSpan.Zero);

        await viewModel.LoadAsync();

        Assert.Equal(50, viewModel.Items.Count);
        Assert.Equal([50], service.SearchLimits);
    }

    [Fact]
    public async Task LoadAsync_with_100_image_items_keeps_first_page_to_50()
    {
        var now = DateTimeOffset.UtcNow;
        var items = Enumerable.Range(0, 100)
            .Select(index => new ClipboardItem(
                Guid.NewGuid(),
                "image/png",
                "png-base64",
                $"图片 {index}",
                $"hash-{index}",
                null,
                "Test",
                "test.exe",
                false,
                false,
                false,
                0,
                now.AddSeconds(-index),
                now.AddSeconds(-index),
                null))
            .ToArray();
        var service = new FakeClipboardService(items)
        {
            SearchHandler = (_, limit, offset, _) => Task.FromResult<IReadOnlyList<ClipboardItem>>(items.Take(limit).ToArray())
        };
        var viewModel = new ClipboardOverlayViewModel(service, TimeSpan.Zero);

        await viewModel.LoadAsync();

        Assert.Equal(50, viewModel.Items.Count);
        Assert.Equal([50], service.SearchLimits);
    }


}
