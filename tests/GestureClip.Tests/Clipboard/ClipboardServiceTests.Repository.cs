using GestureClip.Core.Abstractions;
using GestureClip.Core.Clipboard;
using GestureClip.Core.Settings;
using GestureClip.Core.SystemInfo;
using GestureClip.Features.Clipboard;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using GestureClip.Tests.TestDoubles;

namespace GestureClip.Tests.Clipboard;

public sealed partial class ClipboardServiceTests
{

    private sealed class FakeClipboardRepository : IClipboardRepository
    {
        public List<ClipboardItem> Items { get; } = [];
        public string? BlockedProcessName { get; set; }
        public Guid? IncrementedId { get; private set; }
        public Guid? TouchedId { get; private set; }
        public List<Guid> IncrementedIds { get; } = [];
        public Dictionary<Guid, int> BatchedUseCounts { get; } = [];
        public int BatchIncrementCallCount { get; private set; }
        public Func<ClipboardItem, CancellationToken, Task>? InsertHandler { get; set; }
        public Func<Guid, CancellationToken, Task>? IncrementUseCountHandler { get; set; }
        public bool ReturnPreviewOnlySearchResults { get; set; }

        public Task<ClipboardItem?> FindByHashAsync(string hash, CancellationToken cancellationToken)
        {
            return Task.FromResult(Items.FirstOrDefault(item => item.Hash == hash));
        }

        public Task<ClipboardItem?> GetLatestAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(Items.OrderByDescending(item => item.CreatedAt).FirstOrDefault());
        }

        public Task<ClipboardItem?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
        {
            return Task.FromResult(Items.FirstOrDefault(item => item.Id == id));
        }

        public Task UpdateTextContentAsync(
            Guid id,
            string text,
            string hash,
            string? plainTextHash,
            string preview,
            CancellationToken cancellationToken)
        {
            var index = Items.FindIndex(item => item.Id == id);
            if (index >= 0)
            {
                Items[index] = Items[index] with
                {
                    TextContent = text,
                    PreviewText = preview,
                    Hash = hash,
                    PlainTextHash = plainTextHash
                };
            }

            return Task.CompletedTask;
        }

        public Task TouchAsync(Guid id, CancellationToken cancellationToken)
        {
            TouchedId = id;
            var index = Items.FindIndex(item => item.Id == id);
            if (index >= 0)
            {
                var now = DateTimeOffset.UtcNow;
                Items[index] = Items[index] with { LastUsedAt = now, UpdatedAt = now };
            }

            return Task.CompletedTask;
        }

        public async Task IncrementUseCountAsync(Guid id, CancellationToken cancellationToken)
        {
            if (IncrementUseCountHandler is not null)
            {
                await IncrementUseCountHandler(id, cancellationToken);
            }

            IncrementedId = id;
            IncrementedIds.Add(id);
        }

        public async Task IncrementUseCountsAsync(IReadOnlyDictionary<Guid, int> increments, CancellationToken cancellationToken)
        {
            BatchIncrementCallCount++;
            foreach (var (id, count) in increments)
            {
                for (var index = 0; index < count; index++)
                {
                    await IncrementUseCountAsync(id, cancellationToken);
                }

                BatchedUseCounts[id] = BatchedUseCounts.TryGetValue(id, out var existing)
                    ? existing + count
                    : count;
            }
        }

        public Task<int> DeleteAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken)
        {
            var deleted = Items.RemoveAll(item => ids.Contains(item.Id));
            return Task.FromResult(deleted);
        }

        public Task SetPinnedAsync(Guid id, bool isPinned, CancellationToken cancellationToken)
        {
            var index = Items.FindIndex(item => item.Id == id);
            if (index >= 0)
            {
                Items[index] = Items[index] with { IsPinned = isPinned };
            }

            return Task.CompletedTask;
        }

        public Task SetFavoriteAsync(Guid id, bool isFavorite, CancellationToken cancellationToken)
        {
            var index = Items.FindIndex(item => item.Id == id);
            if (index >= 0)
            {
                Items[index] = Items[index] with { IsFavorite = isFavorite };
            }

            return Task.CompletedTask;
        }

        public async Task InsertAsync(ClipboardItem item, CancellationToken cancellationToken)
        {
            if (InsertHandler is not null)
            {
                await InsertHandler(item, cancellationToken);
            }

            Items.Add(item);
        }

        public Task<bool> IsProcessBlockedAsync(string? processName, CancellationToken cancellationToken)
        {
            return Task.FromResult(!string.IsNullOrWhiteSpace(processName) &&
                string.Equals(processName, BlockedProcessName, StringComparison.OrdinalIgnoreCase));
        }

        public Task<int> GetCountAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(Items.Count);
        }

        public Task<int> ClearAllAsync(CancellationToken cancellationToken)
        {
            var count = Items.Count;
            Items.Clear();
            return Task.FromResult(count);
        }

        public Task<int> ClearUnpinnedAsync(CancellationToken cancellationToken)
        {
            var deleted = Items.RemoveAll(item => !item.IsPinned);
            return Task.FromResult(deleted);
        }

        public Task<int> CleanupAsync(int maxItems, int retentionDays, CancellationToken cancellationToken)
        {
            return Task.FromResult(0);
        }

        public Task<IReadOnlyList<ClipboardItem>> SearchAsync(string keyword, int limit, CancellationToken cancellationToken)
        {
            var results = Items
                .Where(item => string.IsNullOrWhiteSpace(keyword) ||
                    (item.TextContent?.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (item.PreviewText?.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false))
                .Take(limit)
                .Select(item => ReturnPreviewOnlySearchResults ? item with { TextContent = null } : item)
                .ToArray();

            return Task.FromResult<IReadOnlyList<ClipboardItem>>(results);
        }
    }
}
