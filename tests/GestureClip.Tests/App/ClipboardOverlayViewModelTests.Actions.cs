using GestureClip.App.ViewModels;
using GestureClip.Core.Abstractions;
using GestureClip.Core.Clipboard;
using Xunit;

namespace GestureClip.Tests.App;

public sealed partial class ClipboardOverlayViewModelTests
{
    [Fact]
    public async Task DeleteItemsAsync_deletes_selected_items_and_refreshes()
    {
        var first = TextItem("first");
        var second = TextItem("second");
        var service = new FakeClipboardService([first, second]);
        var viewModel = new ClipboardOverlayViewModel(service, TimeSpan.Zero);
        await viewModel.LoadAsync();

        var deleted = await viewModel.DeleteItemsAsync([first]);

        Assert.True(deleted);
        Assert.Equal([first.Id], service.DeletedIds);
        Assert.Equal(["second"], viewModel.Items.Select(item => item.TextContent ?? "").ToArray());
        Assert.Equal("已删除 1 条 · Ctrl+Z 撤销", viewModel.StatusText);
        Assert.True(viewModel.CanUndoDelete);
        Assert.Equal(second.Id, viewModel.SelectedItem?.Id);
    }

    [Fact]
    public async Task UndoLastDeleteAsync_restores_deleted_items()
    {
        var first = TextItem("first");
        var second = TextItem("second");
        var service = new FakeClipboardService([first, second]);
        var viewModel = new ClipboardOverlayViewModel(service, TimeSpan.Zero);
        await viewModel.LoadAsync();

        Assert.True(await viewModel.DeleteItemsAsync([first]));
        Assert.True(viewModel.CanUndoDelete);

        var undone = await viewModel.UndoLastDeleteAsync();

        Assert.True(undone);
        Assert.False(viewModel.CanUndoDelete);
        Assert.Contains(first.Id, viewModel.Items.Select(item => item.Id));
        Assert.Equal("已撤销删除", viewModel.StatusText);
        Assert.Contains(first.Id, service.RestoredIds);
    }

    [Fact]
    public async Task CopySelectedAsPlainTextAsync_cleans_blank_lines()
    {
        var item = TextItem("  hello  \n\n\nworld  \n\n");
        var service = new FakeClipboardService([item]);
        service.FullItems[item.Id] = item;
        var viewModel = new ClipboardOverlayViewModel(service, TimeSpan.Zero);
        await viewModel.LoadAsync();

        var copied = await viewModel.CopySelectedAsPlainTextAsync([item]);

        Assert.True(copied);
        Assert.NotNull(service.LastCopiedItems);
        Assert.Equal("  hello\r\n\r\nworld", service.LastCopiedItems![0].TextContent);
        Assert.Contains("纯文本", viewModel.StatusText);
    }

    [Fact]
    public async Task ResetViewAsync_clears_search_and_filter()
    {
        var item = TextItem("hello world");
        var service = new FakeClipboardService([item]);
        var viewModel = new ClipboardOverlayViewModel(service, TimeSpan.Zero);
        await viewModel.LoadAsync();
        viewModel.SearchText = "hello";
        await WaitForAsync(() => viewModel.Items.Count == 1);
        viewModel.SelectedFilter = ClipboardOverlayFilter.Text;

        await viewModel.ResetViewAsync();

        Assert.Equal("", viewModel.SearchText);
        Assert.Equal(ClipboardOverlayFilter.All, viewModel.SelectedFilter);
        Assert.Equal(item.Id, viewModel.SelectedItem?.Id);
        Assert.Equal("已重置视图", viewModel.StatusText);
    }

    [Fact]
    public async Task CopySelectedAsync_does_not_reload_list_after_copy()
    {
        var first = TextItem("first");
        var second = TextItem("second");
        var service = new FakeClipboardService([first, second]);
        var viewModel = new ClipboardOverlayViewModel(service, TimeSpan.Zero);
        await viewModel.LoadAsync();
        var before = viewModel.Items.Select(item => item.Id).ToArray();
        service.SearchKeywords.Clear();

        var copied = await viewModel.CopySelectedAsync([first]);

        Assert.True(copied);
        Assert.Empty(service.SearchKeywords);
        Assert.Equal(before, viewModel.Items.Select(item => item.Id).ToArray());
        Assert.True(viewModel.Items.Single(item => item.Id == first.Id).IsSessionCopied);
        Assert.False(viewModel.Items.Single(item => item.Id == second.Id).IsSessionCopied);
        Assert.Contains("已复制", viewModel.StatusText);
    }

    [Fact]
    public async Task CopySelectedAsync_marks_only_copied_items_as_session_copied_for_multi_select()
    {
        var first = TextItem("first");
        var second = TextItem("second");
        var third = TextItem("third");
        var service = new FakeClipboardService([first, second, third]);
        var viewModel = new ClipboardOverlayViewModel(service, TimeSpan.Zero);
        await viewModel.LoadAsync();

        Assert.True(await viewModel.CopySelectedAsync([first, third]));

        Assert.True(viewModel.Items.Single(item => item.Id == first.Id).IsSessionCopied);
        Assert.False(viewModel.Items.Single(item => item.Id == second.Id).IsSessionCopied);
        Assert.True(viewModel.Items.Single(item => item.Id == third.Id).IsSessionCopied);
    }

    [Fact]
    public async Task CopySelectedAsync_handles_20_fast_clicks_without_reloading()
    {
        var items = Enumerable.Range(0, 50).Select(index => TextItem($"item {index}")).ToArray();
        var service = new FakeClipboardService(items);
        var viewModel = new ClipboardOverlayViewModel(service, TimeSpan.Zero);
        await viewModel.LoadAsync();
        service.SearchKeywords.Clear();

        foreach (var item in viewModel.Items.Take(20).ToArray())
        {
            Assert.True(await viewModel.CopySelectedAsync([item]));
        }

        Assert.Empty(service.SearchKeywords);
        Assert.Equal(20, service.CopyCount);
        Assert.Equal(20, viewModel.Items.Count(item => item.IsSessionCopied));
        Assert.All(viewModel.Items.Take(20), item => Assert.Equal(1, item.UseCount));
        Assert.Contains("已复制", viewModel.StatusText);
    }

    [Fact]
    public async Task TogglePinnedAsync_updates_selected_item_and_refreshes()
    {
        var item = TextItem("pin me");
        var service = new FakeClipboardService([item]);
        var viewModel = new ClipboardOverlayViewModel(service, TimeSpan.Zero);
        await viewModel.LoadAsync();

        var updated = await viewModel.ToggleSelectedPinnedAsync();

        Assert.True(updated);
        Assert.Equal((item.Id, true), service.PinnedUpdates.Single());
        Assert.True(viewModel.Items.Single().IsPinned);
        Assert.Equal("已置顶", viewModel.StatusText);
    }


    [Fact]
    public async Task CopySelectedAsync_handles_100_fast_clicks_without_reloading_or_growing_items()
    {
        var item = TextItem("repeat");
        var service = new FakeClipboardService([item]);
        var viewModel = new ClipboardOverlayViewModel(service, TimeSpan.Zero);
        await viewModel.LoadAsync();
        service.SearchKeywords.Clear();

        for (var index = 0; index < 100; index++)
        {
            Assert.True(await viewModel.CopySelectedAsync([item]));
        }

        Assert.Equal(100, service.CopyCount);
        Assert.Empty(service.SearchKeywords);
        Assert.Single(viewModel.Items);
        Assert.Equal(100, viewModel.Items[0].UseCount);
        Assert.True(viewModel.Items[0].IsSessionCopied);
        Assert.Contains("已复制", viewModel.StatusText);
    }

    [Fact]
    public async Task ToggleFavoriteAsync_updates_selected_item_and_refreshes()
    {
        var item = TextItem("template");
        var service = new FakeClipboardService([item]);
        var viewModel = new ClipboardOverlayViewModel(service, TimeSpan.Zero);
        await viewModel.LoadAsync();

        var updated = await viewModel.ToggleSelectedFavoriteAsync();

        Assert.True(updated);
        Assert.Equal((item.Id, true), service.FavoriteUpdates.Single());
        Assert.True(viewModel.Items.Single().IsFavorite);
        Assert.Equal("已保存为片段", viewModel.StatusText);
    }

}
