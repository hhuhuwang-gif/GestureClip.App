using GestureClip.App.ViewModels;
using GestureClip.Core.Abstractions;
using GestureClip.Core.Clipboard;
using Xunit;

namespace GestureClip.Tests.App;

public sealed partial class ClipboardOverlayViewModelTests
{
    [Fact]
    public async Task Filter_shows_only_matching_clipboard_items()
    {
        var pinnedText = TextItem("pinned") with { IsPinned = true };
        var favoriteText = TextItem("favorite") with { IsFavorite = true };
        var normalText = TextItem("normal");
        var pngImage = new ClipboardItem(
            Guid.NewGuid(),
            "image/png",
            "png-base64",
            "图片",
            "image-hash",
            null,
            "SnippingTool",
            "snippingtool.exe",
            false,
            false,
            false,
            0,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            null);
        var jpegImage = ImageItem("JPEG 图片", "image/jpeg");
        var viewModel = new ClipboardOverlayViewModel(new FakeClipboardService([pinnedText, favoriteText, normalText, pngImage, jpegImage]), TimeSpan.Zero);
        await viewModel.LoadAsync();

        viewModel.SelectedFilter = ClipboardOverlayFilter.Pinned;
        await WaitForAsync(() => viewModel.Items.Count == 1 && viewModel.Items.FirstOrDefault()?.PreviewText == "pinned");
        Assert.Equal(["pinned"], viewModel.Items.Select(item => item.PreviewText ?? "").ToArray());

        viewModel.SelectedFilter = ClipboardOverlayFilter.Favorites;
        await WaitForAsync(() => viewModel.Items.Count == 1 && viewModel.Items.FirstOrDefault()?.PreviewText == "favorite");
        Assert.Equal(["favorite"], viewModel.Items.Select(item => item.PreviewText ?? "").ToArray());

        viewModel.SelectedFilter = ClipboardOverlayFilter.Images;
        await WaitForAsync(() => viewModel.Items.Count == 2);
        Assert.Equal(["JPEG 图片", "图片"], viewModel.Items.Select(item => item.PreviewText ?? "").ToArray());

        viewModel.SelectedFilter = ClipboardOverlayFilter.Text;
        await WaitForAsync(() => viewModel.Items.Count == 3 && viewModel.Items.All(item => item.ContentType == "text"));
        Assert.Equal(["pinned", "normal", "favorite"], viewModel.Items.Select(item => item.PreviewText ?? "").ToArray());

        viewModel.SelectedFilter = ClipboardOverlayFilter.All;
        await WaitForAsync(() => viewModel.Items.Count == 5);
        Assert.Equal(["pinned", "JPEG 图片", "图片", "normal", "favorite"], viewModel.Items.Select(item => item.PreviewText ?? "").ToArray());
    }

    [Fact]
    public async Task SelectedFilter_queries_database_filter_instead_of_current_page()
    {
        var textItems = Enumerable.Range(0, 50)
            .Select(index => TextItem($"text {index:000}"))
            .ToArray();
        var image = ImageItem("图片 001");
        var service = new FakeClipboardService(textItems)
        {
            SearchHandlerWithFilter = (_, _, _, filter, _) =>
            {
                return Task.FromResult<IReadOnlyList<ClipboardItem>>(
                    filter == ClipboardContentFilter.Images ? [image] : textItems);
            }
        };
        var viewModel = new ClipboardOverlayViewModel(service, TimeSpan.Zero);

        await viewModel.LoadAsync();
        viewModel.SelectedFilter = ClipboardOverlayFilter.Images;
        await WaitForAsync(() =>
            viewModel.Items.Count == 1 &&
            viewModel.Items[0].PreviewText == "图片 001" &&
            service.SearchFilters.Contains(ClipboardContentFilter.Images));

        Assert.Equal([ClipboardContentFilter.All, ClipboardContentFilter.Images], service.SearchFilters);
        Assert.Equal(["图片 001"], viewModel.Items.Select(item => item.PreviewText ?? "").ToArray());
    }

    [Fact]
    public async Task LoadMoreAsync_uses_selected_database_filter()
    {
        var firstPage = Enumerable.Range(0, 50)
            .Select(index => ImageItem($"图片 {index:000}"))
            .ToArray();
        var secondPage = Enumerable.Range(50, 10)
            .Select(index => ImageItem($"图片 {index:000}"))
            .ToArray();
        var service = new FakeClipboardService(firstPage.Concat(secondPage).ToArray())
        {
            SearchHandlerWithFilter = (_, limit, offset, filter, _) =>
            {
                Assert.Equal(ClipboardContentFilter.Images, filter);
                return Task.FromResult<IReadOnlyList<ClipboardItem>>(
                    offset == 0 ? firstPage.Take(limit).ToArray() : secondPage.Take(limit).ToArray());
            }
        };
        var viewModel = new ClipboardOverlayViewModel(service, TimeSpan.Zero);

        viewModel.SelectedFilter = ClipboardOverlayFilter.Images;
        await WaitForAsync(() => viewModel.Items.Count == 50);
        await viewModel.LoadMoreAsync();

        Assert.Equal([0, 50], service.SearchOffsets);
        Assert.Equal([ClipboardContentFilter.Images, ClipboardContentFilter.Images], service.SearchFilters);
        Assert.Equal(60, viewModel.Items.Count);
    }

    [Fact]
    public async Task ClearSearchAsync_clears_search_text_and_refreshes_results()
    {
        var service = new FakeClipboardService([TextItem("alpha"), TextItem("beta")]);
        var viewModel = new ClipboardOverlayViewModel(service, TimeSpan.Zero);
        viewModel.SearchText = "alpha";
        await WaitForAsync(() => service.SearchKeywords.Contains("alpha"));

        var cleared = await viewModel.ClearSearchAsync();

        Assert.True(cleared);
        Assert.Equal("", viewModel.SearchText);
        Assert.Contains("", service.SearchKeywords);
    }

    [Fact]
    public async Task ClearSearchAsync_restores_recent_history_first_page_without_full_reload()
    {
        var items = Enumerable.Range(0, 1000)
            .Select(index => TextItem($"item {index:0000}"))
            .ToArray();
        var service = new FakeClipboardService(items);
        var viewModel = new ClipboardOverlayViewModel(service, TimeSpan.Zero);
        viewModel.SearchText = "item 0001";
        await WaitForAsync(() => service.SearchKeywords.Contains("item 0001"));

        await viewModel.ClearSearchAsync();

        Assert.Equal("", viewModel.SearchText);
        Assert.Equal(50, viewModel.Items.Count);
        Assert.Equal(50, service.SearchLimits.Last());
        Assert.Equal(0, service.SearchOffsets.Last());
    }

}
