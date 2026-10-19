using System.Collections.Specialized;
using GestureClip.App.ViewModels;
using GestureClip.Core.Abstractions;
using GestureClip.Core.Clipboard;
using Xunit;

namespace GestureClip.Tests.App;

public sealed class ClipboardOverlayResponsivenessTests
{
    [Fact]
    public async Task Smart_search_reuses_pool_until_history_changes()
    {
        var service = new SearchService([]);
        var viewModel = new ClipboardOverlayViewModel(service, TimeSpan.FromDays(1));

        viewModel.SearchText = "wx";
        await viewModel.SearchAsync();
        viewModel.SearchText = "zfb";
        await viewModel.SearchAsync();
        Assert.Equal(1, service.PoolReads);

        service.HistoryRevision++;
        await viewModel.SearchAsync();
        Assert.Equal(2, service.PoolReads);
    }

    [Fact]
    public async Task Smart_search_does_not_reuse_pool_from_another_filter()
    {
        var service = new SearchService([]);
        var viewModel = new ClipboardOverlayViewModel(service, TimeSpan.FromDays(1));
        viewModel.SearchText = "wx";
        await viewModel.SearchAsync();

        viewModel.SelectedFilter = ClipboardOverlayFilter.Text;
        await viewModel.SearchAsync();

        Assert.Equal(ClipboardContentFilter.Text, service.LastPoolFilter);
    }

    [Fact]
    public async Task Load_more_keeps_existing_rows_and_multiple_selection_without_reset()
    {
        const int itemCount = 75;
        var service = new SearchService(Enumerable.Range(0, itemCount).Select(CreateItem).ToArray());
        var viewModel = new ClipboardOverlayViewModel(service);
        await viewModel.LoadAsync();
        var selected = viewModel.Items[5];
        viewModel.SelectedItem = selected;
        viewModel.UpdateSelectedCount(2);
        var resets = 0;
        viewModel.Items.CollectionChanged += (_, args) =>
        {
            if (args.Action == NotifyCollectionChangedAction.Reset) resets++;
        };

        await viewModel.LoadMoreAsync();

        Assert.Equal(itemCount, viewModel.Items.Count);
        Assert.Equal(0, resets);
        Assert.Same(selected, viewModel.SelectedItem);
        Assert.Contains("已选 2 条", viewModel.SummaryText);
    }

    private static ClipboardItem CreateItem(int index)
    {
        var now = DateTimeOffset.UtcNow;
        return new ClipboardItem(Guid.NewGuid(), "text", $"item {index}", $"item {index}",
            $"hash{index}", null, "Test", "test.exe", false, false, false, 0, now, now, null);
    }

    [Fact]
    public async Task Plain_text_copy_reuses_loaded_content_without_reading_each_row_again()
    {
        var item = CreateItem(1);
        var service = new SearchService([item]);
        var viewModel = new ClipboardOverlayViewModel(service);
        await viewModel.LoadAsync();

        Assert.True(await viewModel.CopySelectedAsPlainTextAsync([viewModel.Items[0]]));

        Assert.Equal(0, service.FullItemReads);
        Assert.Equal(item.TextContent, Assert.Single(service.LastCopiedItems).TextContent);
    }

    [Fact]
    public async Task Plain_text_copy_fetches_content_when_only_a_preview_is_loaded()
    {
        var item = CreateItem(2);
        var service = new SearchService([item]);
        var viewModel = new ClipboardOverlayViewModel(service);

        Assert.True(await viewModel.CopySelectedAsPlainTextAsync([item with { TextContent = null }]));

        Assert.Equal(1, service.FullItemReads);
        Assert.Equal(item.TextContent, Assert.Single(service.LastCopiedItems).TextContent);
    }

    [Fact]
    public async Task Paste_flushes_pending_search_and_preserves_plain_text_option()
    {
        var service = new SearchService([CreateItem(1), CreateItem(2)]);
        var viewModel = new ClipboardOverlayViewModel(service, TimeSpan.FromDays(1));
        await viewModel.LoadAsync();
        viewModel.SearchText = "item 2";

        var pasted = await viewModel.PasteSelectedAsync(keepOverlayOpen: true, plainText: true);

        Assert.True(pasted);
        Assert.Equal("item 2", service.LastPastedItem?.TextContent);
        Assert.True(service.LastPasteOptions?.PlainText);
        Assert.Contains("面板保持打开", viewModel.StatusText);
    }

    [Fact]
    public async Task Flush_joins_running_query_without_reading_database_twice()
    {
        var service = new SearchService([CreateItem(2)])
        {
            SearchStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously),
            SearchRelease = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously)
        };
        var viewModel = new ClipboardOverlayViewModel(service, TimeSpan.Zero);
        viewModel.SearchText = "item 2";
        await service.SearchStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var flush = viewModel.FlushPendingSearchAsync();
        service.SearchRelease.SetResult();
        await flush;

        Assert.Equal(1, service.PrimaryReads);
        Assert.Single(viewModel.Items);
    }

    [Fact]
    public async Task Transient_paste_failure_does_not_block_the_next_attempt()
    {
        var service = new SearchService([CreateItem(1)]) { FailNextPaste = true };
        var viewModel = new ClipboardOverlayViewModel(service);
        await viewModel.LoadAsync();

        Assert.False(await viewModel.PasteSelectedAsync());
        Assert.True(viewModel.HasError);
        Assert.False(viewModel.HasSearchError);
        Assert.True(await viewModel.PasteSelectedAsync());
        Assert.False(viewModel.HasError);
    }

    [Fact]
    public async Task Failed_search_never_pastes_the_previous_results_and_can_be_retried()
    {
        var service = new SearchService([CreateItem(1), CreateItem(2)]);
        var viewModel = new ClipboardOverlayViewModel(service, TimeSpan.FromDays(1));
        await viewModel.LoadAsync();
        service.SearchFailure = new InvalidOperationException("database busy");
        viewModel.SearchText = "item 2";
        await viewModel.SearchAsync();

        Assert.True(viewModel.HasSearchError);
        Assert.False(await viewModel.PasteSelectedAsync());
        Assert.Null(service.LastPastedItem);

        service.SearchFailure = null;
        await viewModel.SearchAsync();
        Assert.True(await viewModel.PasteSelectedAsync());
        Assert.Equal("item 2", service.LastPastedItem?.TextContent);
    }

    private sealed class SearchService(IReadOnlyList<ClipboardItem> items) : IClipboardService
    {
        public int PoolReads { get; private set; }
        public int PrimaryReads { get; private set; }
        public int FullItemReads { get; private set; }
        public IReadOnlyList<ClipboardItem> LastCopiedItems { get; private set; } = [];
        public bool FailNextPaste { get; set; }
        public Exception? SearchFailure { get; set; }
        public TaskCompletionSource? SearchStarted { get; init; }
        public TaskCompletionSource? SearchRelease { get; init; }
        public ClipboardContentFilter LastPoolFilter { get; private set; }
        public long HistoryRevision { get; set; }
        public ClipboardItem? LastPastedItem { get; private set; }
        public PasteOptions? LastPasteOptions { get; private set; }
        public bool IsCaptureEnabled => true;
        public DateTimeOffset? SuppressCaptureUntil => null;
        public Task StartAsync(CancellationToken token) => Task.CompletedTask;
        public Task StopAsync(CancellationToken token) => Task.CompletedTask;
        public Task SetCaptureEnabledAsync(bool enabled, CancellationToken token) => Task.CompletedTask;
        public void SuppressCaptureFor(TimeSpan duration) { }
        public Task CaptureTextAsync(ClipboardCapture capture, CancellationToken token) => Task.CompletedTask;
        public Task<ClipboardItem?> GetLatestAsync(CancellationToken token) => Task.FromResult<ClipboardItem?>(null);
        public Task PasteAsync(ClipboardItem item, PasteOptions options, CancellationToken token)
        {
            if (FailNextPaste)
            {
                FailNextPaste = false;
                throw new InvalidOperationException("clipboard busy");
            }
            LastPastedItem = item;
            LastPasteOptions = options;
            return Task.CompletedTask;
        }
        public Task CopyItemsAsync(IReadOnlyList<ClipboardItem> copied, CancellationToken token)
        {
            LastCopiedItems = copied;
            return Task.CompletedTask;
        }
        public Task<ClipboardItem?> GetByIdAsync(Guid id, CancellationToken token)
        {
            FullItemReads++;
            return Task.FromResult(items.FirstOrDefault(item => item.Id == id));
        }
        public Task<int> DeleteItemsAsync(IReadOnlyList<Guid> ids, CancellationToken token) => Task.FromResult(ids.Count);
        public Task SetPinnedAsync(Guid id, bool pinned, CancellationToken token) => Task.CompletedTask;
        public Task SetFavoriteAsync(Guid id, bool favorite, CancellationToken token) => Task.CompletedTask;

        public Task<IReadOnlyList<ClipboardItem>> SearchAsync(string keyword, int limit, CancellationToken token) =>
            SearchAsync(keyword, limit, 0, ClipboardContentFilter.All, token);

        public async Task<IReadOnlyList<ClipboardItem>> SearchAsync(string keyword, int limit, int offset,
            ClipboardContentFilter filter, CancellationToken token)
        {
            if (SearchFailure is { } error) throw error;
            if (keyword.Length == 0 && limit > 50)
            {
                PoolReads++;
                LastPoolFilter = filter;
            }
            else
            {
                PrimaryReads++;
                SearchStarted?.TrySetResult();
                if (SearchRelease is not null) await SearchRelease.Task.WaitAsync(token);
            }

            return items
                .Where(item => keyword.Length == 0 || (item.TextContent ?? "").Contains(keyword))
                .Skip(offset).Take(limit).ToArray();
        }
    }
}
