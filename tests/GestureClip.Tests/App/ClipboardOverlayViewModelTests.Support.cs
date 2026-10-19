using GestureClip.App.ViewModels;
using GestureClip.Core.Abstractions;
using GestureClip.Core.Clipboard;
using Xunit;

namespace GestureClip.Tests.App;

public sealed partial class ClipboardOverlayViewModelTests
{
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

    private static ClipboardItem ImageItem(string previewText, string contentType = "image/png")
    {
        var now = DateTimeOffset.UtcNow;
        return new ClipboardItem(
            Guid.NewGuid(),
            contentType,
            null,
            previewText,
            $"hash-{previewText}",
            null,
            "Test",
            "test.exe",
            false,
            false,
            false,
            0,
            now,
            now,
            null,
            $"thumb-{previewText}");
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

        public long HistoryRevision { get; set; }

        public List<string> SearchKeywords { get; } = [];

        public List<int> SearchLimits { get; } = [];

        public List<int> SearchOffsets { get; } = [];

        public List<ClipboardContentFilter> SearchFilters { get; } = [];

        public List<Guid> DeletedIds { get; } = [];

        public List<Guid> RestoredIds { get; } = [];

        public Dictionary<Guid, ClipboardItem> FullItems { get; } = [];

        public List<Guid> GetByIdRequests { get; } = [];

        public List<(Guid Id, bool IsPinned)> PinnedUpdates { get; } = [];

        public List<(Guid Id, bool IsFavorite)> FavoriteUpdates { get; } = [];

        public int CopyCount { get; private set; }

        public IReadOnlyList<ClipboardItem>? LastCopiedItems { get; private set; }

        public Func<string, int, int, CancellationToken, Task<IReadOnlyList<ClipboardItem>>>? SearchHandler { get; set; }

        public Func<string, int, int, ClipboardContentFilter, CancellationToken, Task<IReadOnlyList<ClipboardItem>>>? SearchHandlerWithFilter { get; set; }

        public Func<ClipboardItem, PasteOptions, CancellationToken, Task>? PasteHandler { get; set; }

        public Func<IReadOnlyList<ClipboardItem>, CancellationToken, Task>? CopyHandler { get; set; }

        public DateTimeOffset? SuppressCaptureUntil => null;

        public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task SetCaptureEnabledAsync(bool enabled, CancellationToken cancellationToken) => Task.CompletedTask;

        public void SuppressCaptureFor(TimeSpan duration) { }

        public Task CaptureTextAsync(ClipboardCapture capture, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<IReadOnlyList<ClipboardItem>> SearchAsync(string keyword, int limit, CancellationToken cancellationToken)
        {
            return SearchAsync(keyword, limit, 0, cancellationToken);
        }

        public Task<IReadOnlyList<ClipboardItem>> SearchAsync(string keyword, int limit, int offset, CancellationToken cancellationToken)
        {
            return SearchAsync(keyword, limit, offset, ClipboardContentFilter.All, cancellationToken);
        }

        public Task<IReadOnlyList<ClipboardItem>> SearchAsync(string keyword, int limit, int offset, ClipboardContentFilter filter, CancellationToken cancellationToken)
        {
            SearchKeywords.Add(keyword);
            SearchLimits.Add(limit);
            SearchOffsets.Add(offset);
            SearchFilters.Add(filter);
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

            if (SearchHandlerWithFilter is not null)
            {
                return SearchHandlerWithFilter(keyword, limit, offset, filter, cancellationToken);
            }

            return SearchHandler is null
                ? Task.FromResult<IReadOnlyList<ClipboardItem>>(visibleItems.Where(item => MatchesFilter(item, filter)).Skip(offset).Take(limit).ToArray())
                : SearchHandler(keyword, limit, offset, cancellationToken);
        }

        private static bool MatchesFilter(ClipboardItem item, ClipboardContentFilter filter)
        {
            return filter switch
            {
                ClipboardContentFilter.Pinned => item.IsPinned,
                ClipboardContentFilter.Favorites => item.IsFavorite,
                ClipboardContentFilter.Text => item.IsText,
                ClipboardContentFilter.Images => item.IsImage,
                _ => true
            };
        }

        public Task<ClipboardItem?> GetLatestAsync(CancellationToken cancellationToken) => Task.FromResult<ClipboardItem?>(null);

        public Task<ClipboardItem?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
        {
            GetByIdRequests.Add(id);
            return Task.FromResult(FullItems.TryGetValue(id, out var item) ? item : null);
        }

        public Task PasteAsync(ClipboardItem item, PasteOptions options, CancellationToken cancellationToken)
        {
            return PasteHandler is null
                ? Task.CompletedTask
                : PasteHandler(item, options, cancellationToken);
        }

        public Task CopyItemsAsync(IReadOnlyList<ClipboardItem> items, CancellationToken cancellationToken)
        {
            CopyCount++;
            LastCopiedItems = items.ToArray();
            return CopyHandler is null
                ? Task.CompletedTask
                : CopyHandler(items, cancellationToken);
        }

        public Task<int> DeleteItemsAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken)
        {
            DeletedIds.AddRange(ids);
            return Task.FromResult(ids.Count);
        }

        public Task RestoreItemsAsync(IReadOnlyList<ClipboardItem> items, CancellationToken cancellationToken)
        {
            foreach (var item in items)
            {
                RestoredIds.Add(item.Id);
                DeletedIds.RemoveAll(id => id == item.Id);
            }

            return Task.CompletedTask;
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
