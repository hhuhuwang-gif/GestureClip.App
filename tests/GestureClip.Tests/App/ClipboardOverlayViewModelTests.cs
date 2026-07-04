using GestureClip.App.ViewModels;
using GestureClip.Core.Abstractions;
using GestureClip.Core.Clipboard;
using Xunit;

namespace GestureClip.Tests.App;

public sealed class ClipboardOverlayViewModelTests
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
        var image = new ClipboardItem(
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

        viewModel.SelectedItem = image;

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

        await WaitForAsync(() => service.SearchKeywords.Count == 1);

        Assert.Equal(["abc"], service.SearchKeywords);
        Assert.Equal([50], service.SearchLimits);
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
    public async Task LoadAsync_sets_error_message_when_search_fails()
    {
        var service = new FakeClipboardService([])
        {
            SearchHandler = (_, _, _) => throw new InvalidOperationException("database busy")
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
            SearchHandler = async (keyword, _, cancellationToken) =>
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
        Assert.Equal("已删除 1 条", viewModel.StatusText);
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

    [Fact]
    public async Task Filter_shows_only_matching_clipboard_items()
    {
        var pinnedText = TextItem("pinned") with { IsPinned = true };
        var favoriteText = TextItem("favorite") with { IsFavorite = true };
        var normalText = TextItem("normal");
        var image = new ClipboardItem(
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
        var viewModel = new ClipboardOverlayViewModel(new FakeClipboardService([pinnedText, favoriteText, normalText, image]), TimeSpan.Zero);
        await viewModel.LoadAsync();

        viewModel.SelectedFilter = ClipboardOverlayFilter.Pinned;
        Assert.Equal(["pinned"], viewModel.Items.Select(item => item.PreviewText ?? "").ToArray());

        viewModel.SelectedFilter = ClipboardOverlayFilter.Favorites;
        Assert.Equal(["favorite"], viewModel.Items.Select(item => item.PreviewText ?? "").ToArray());

        viewModel.SelectedFilter = ClipboardOverlayFilter.Images;
        Assert.Equal(["图片"], viewModel.Items.Select(item => item.PreviewText ?? "").ToArray());

        viewModel.SelectedFilter = ClipboardOverlayFilter.Text;
        Assert.Equal(["pinned", "normal", "favorite"], viewModel.Items.Select(item => item.PreviewText ?? "").ToArray());

        viewModel.SelectedFilter = ClipboardOverlayFilter.All;
        Assert.Equal(["pinned", "图片", "normal", "favorite"], viewModel.Items.Select(item => item.PreviewText ?? "").ToArray());
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
    public async Task SummaryText_shows_item_count_and_selected_count()
    {
        var viewModel = new ClipboardOverlayViewModel(
            new FakeClipboardService([TextItem("first"), TextItem("second")]),
            TimeSpan.Zero);

        await viewModel.LoadAsync();

        Assert.Equal("共 2 条 · 已选 1 条", viewModel.SummaryText);

        viewModel.UpdateSelectedCount(2);

        Assert.Equal("共 2 条 · 已选 2 条", viewModel.SummaryText);
    }

    [Fact]
    public async Task PasteSelectedAsync_shows_error_without_throwing_when_clipboard_write_fails()
    {
        var item = TextItem("paste me");
        var service = new FakeClipboardService([item])
        {
            PasteHandler = (_, _, _) => throw new InvalidOperationException("clipboard busy")
        };
        var viewModel = new ClipboardOverlayViewModel(service, TimeSpan.Zero);
        await viewModel.LoadAsync();

        var pasted = await viewModel.PasteSelectedAsync();

        Assert.False(pasted);
        Assert.True(viewModel.HasError);
        Assert.Contains("clipboard busy", viewModel.ErrorMessage);
        Assert.Equal("粘贴失败", viewModel.StatusText);
    }

    private static ClipboardItem TextItem(string text)
    {
        var now = DateTimeOffset.UtcNow;
        return new ClipboardItem(
            Guid.NewGuid(),
            "text",
            text,
            text,
            text,
            text,
            "Test",
            "test.exe",
            false,
            false,
            false,
            0,
            now,
            now,
            null);
    }

    private static async Task WaitForAsync(Func<bool> condition)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(2);
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(10);
        }

        Assert.True(condition());
    }

    private sealed class FakeClipboardService : IClipboardService
    {
        private readonly IReadOnlyList<ClipboardItem> _items;

        public FakeClipboardService(IReadOnlyList<ClipboardItem> items)
        {
            _items = items;
        }

        public bool IsCaptureEnabled => true;

        public List<string> SearchKeywords { get; } = [];

        public List<int> SearchLimits { get; } = [];

        public List<Guid> DeletedIds { get; } = [];

        public List<(Guid Id, bool IsPinned)> PinnedUpdates { get; } = [];

        public List<(Guid Id, bool IsFavorite)> FavoriteUpdates { get; } = [];

        public Func<string, int, CancellationToken, Task<IReadOnlyList<ClipboardItem>>>? SearchHandler { get; set; }

        public Func<ClipboardItem, PasteOptions, CancellationToken, Task>? PasteHandler { get; set; }

        public DateTimeOffset? SuppressCaptureUntil => null;

        public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task SetCaptureEnabledAsync(bool enabled, CancellationToken cancellationToken) => Task.CompletedTask;

        public void SuppressCaptureFor(TimeSpan duration) { }

        public Task CaptureTextAsync(ClipboardCapture capture, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<IReadOnlyList<ClipboardItem>> SearchAsync(string keyword, int limit, CancellationToken cancellationToken)
        {
            SearchKeywords.Add(keyword);
            SearchLimits.Add(limit);
            var visibleItems = _items.Where(item => !DeletedIds.Contains(item.Id))
                .Select(item =>
                {
                    var pinnedUpdate = PinnedUpdates.LastOrDefault(update => update.Id == item.Id);
                    var favoriteUpdate = FavoriteUpdates.LastOrDefault(update => update.Id == item.Id);
                    var updated = pinnedUpdate.Id == item.Id
                        ? item with { IsPinned = pinnedUpdate.IsPinned }
                        : item;
                    return favoriteUpdate.Id == item.Id
                        ? updated with { IsFavorite = favoriteUpdate.IsFavorite }
                        : updated;
                })
                .OrderByDescending(item => item.IsPinned)
                .ThenByDescending(item => item.CreatedAt)
                .ToArray();

            return SearchHandler is null
                ? Task.FromResult<IReadOnlyList<ClipboardItem>>(visibleItems)
                : SearchHandler(keyword, limit, cancellationToken);
        }

        public Task<ClipboardItem?> GetLatestAsync(CancellationToken cancellationToken) => Task.FromResult<ClipboardItem?>(null);

        public Task PasteAsync(ClipboardItem item, PasteOptions options, CancellationToken cancellationToken)
        {
            return PasteHandler is null
                ? Task.CompletedTask
                : PasteHandler(item, options, cancellationToken);
        }

        public Task CopyItemsAsync(IReadOnlyList<ClipboardItem> items, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<int> DeleteItemsAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken)
        {
            DeletedIds.AddRange(ids);
            return Task.FromResult(ids.Count);
        }

        public Task SetPinnedAsync(Guid id, bool isPinned, CancellationToken cancellationToken)
        {
            PinnedUpdates.Add((id, isPinned));
            return Task.CompletedTask;
        }

        public Task SetFavoriteAsync(Guid id, bool isFavorite, CancellationToken cancellationToken)
        {
            FavoriteUpdates.Add((id, isFavorite));
            return Task.CompletedTask;
        }
    }
}
