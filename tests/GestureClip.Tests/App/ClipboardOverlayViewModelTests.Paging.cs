using GestureClip.App.ViewModels;
using GestureClip.Core.Abstractions;
using GestureClip.Core.Clipboard;
using Xunit;

namespace GestureClip.Tests.App;

public sealed partial class ClipboardOverlayViewModelTests
{
    [Fact]
    public async Task LoadMoreAsync_appends_next_page_without_replacing_existing_items()
    {
        var items = Enumerable.Range(0, 120)
            .Select(index => TextItem($"item {index:000}"))
            .ToArray();
        var service = new FakeClipboardService(items)
        {
            SearchHandler = (_, limit, offset, _) => Task.FromResult<IReadOnlyList<ClipboardItem>>(items.Skip(offset).Take(limit).ToArray())
        };
        var viewModel = new ClipboardOverlayViewModel(service, TimeSpan.Zero);

        await viewModel.LoadAsync();
        await viewModel.LoadMoreAsync();

        Assert.Equal(100, viewModel.Items.Count);
        Assert.Equal([0, 50], service.SearchOffsets);
        Assert.True(viewModel.HasMoreItems);
        Assert.Equal("item 000", viewModel.Items[0].TextContent);
        Assert.Equal("item 099", viewModel.Items[^1].TextContent);
    }

    [Fact]
    public async Task LoadMoreAsync_marks_no_more_items_when_short_page_returns()
    {
        var items = Enumerable.Range(0, 70)
            .Select(index => TextItem($"item {index:000}"))
            .ToArray();
        var service = new FakeClipboardService(items)
        {
            SearchHandler = (_, limit, offset, _) => Task.FromResult<IReadOnlyList<ClipboardItem>>(items.Skip(offset).Take(limit).ToArray())
        };
        var viewModel = new ClipboardOverlayViewModel(service, TimeSpan.Zero);

        await viewModel.LoadAsync();
        await viewModel.LoadMoreAsync();

        Assert.Equal(70, viewModel.Items.Count);
        Assert.False(viewModel.HasMoreItems);
        Assert.Equal([0, 50], service.SearchOffsets);
    }

    [Fact]
    public async Task LoadMoreAsync_keeps_search_keyword_when_loading_next_page()
    {
        var items = Enumerable.Range(0, 75)
            .Select(index => TextItem($"alpha item {index:000}"))
            .ToArray();
        var service = new FakeClipboardService(items)
        {
            SearchHandler = (keyword, limit, offset, _) =>
                Task.FromResult<IReadOnlyList<ClipboardItem>>(
                    items.Where(item => item.TextContent!.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                        .Skip(offset)
                        .Take(limit)
                        .ToArray())
        };
        var viewModel = new ClipboardOverlayViewModel(service, TimeSpan.Zero);

        viewModel.SearchText = "alpha";
        await WaitForAsync(() => viewModel.Items.Count == 50);
        await viewModel.LoadMoreAsync();

        Assert.Equal(["alpha", "alpha"], service.SearchKeywords);
        Assert.Equal([0, 50], service.SearchOffsets);
        Assert.Equal(75, viewModel.Items.Count);
        Assert.False(viewModel.HasMoreItems);
    }

    [Fact]
    public async Task LoadAsync_sets_error_message_when_search_fails()
    {
        var service = new FakeClipboardService([])
        {
            SearchHandler = (_, _, _, _) => throw new InvalidOperationException("database busy")
        };
        var viewModel = new ClipboardOverlayViewModel(service, TimeSpan.Zero);

        await viewModel.LoadAsync();

        Assert.True(viewModel.HasError);
        Assert.Contains("database busy", viewModel.ErrorMessage);
        Assert.Equal("加载失败", viewModel.StatusText);
    }

    [Fact]
    public async Task Older_search_result_does_not_replace_newer_result()
    {
        var oldItem = TextItem("old");
        var newItem = TextItem("new");
        var service = new FakeClipboardService([])
        {
            SearchHandler = async (keyword, _, _, cancellationToken) =>
            {
                if (keyword == "old")
                {
                    await Task.Delay(120, cancellationToken);
                    return [oldItem];
                }

                await Task.Delay(10, cancellationToken);
                return [newItem];
            }
        };
        var viewModel = new ClipboardOverlayViewModel(service, TimeSpan.Zero);

        viewModel.SearchText = "old";
        await Task.Delay(20);
        viewModel.SearchText = "new";

        await WaitForAsync(() => viewModel.Items.Count == 1 && viewModel.Items[0].PreviewText == "new");
        await Task.Delay(160);

        Assert.Single(viewModel.Items);
        Assert.Equal("new", viewModel.Items[0].PreviewText);
    }


}
