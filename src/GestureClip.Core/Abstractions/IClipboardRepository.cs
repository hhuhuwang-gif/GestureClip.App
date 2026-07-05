using GestureClip.Core.Clipboard;

namespace GestureClip.Core.Abstractions;

public interface IClipboardRepository
{
    Task<ClipboardItem?> FindByHashAsync(string hash, CancellationToken cancellationToken);

    Task InsertAsync(ClipboardItem item, CancellationToken cancellationToken);

    Task<IReadOnlyList<ClipboardItem>> SearchAsync(string keyword, int limit, CancellationToken cancellationToken);

    Task<IReadOnlyList<ClipboardItem>> SearchAsync(string keyword, int limit, int offset, CancellationToken cancellationToken)
    {
        return offset <= 0
            ? SearchAsync(keyword, limit, cancellationToken)
            : Task.FromResult<IReadOnlyList<ClipboardItem>>([]);
    }

    async Task<IReadOnlyList<ClipboardItem>> SearchAsync(
        string keyword,
        int limit,
        int offset,
        ClipboardContentFilter filter,
        CancellationToken cancellationToken)
    {
        var results = await SearchAsync(keyword, limit, offset, cancellationToken);
        return results.Where(item => MatchesFilter(item, filter)).ToArray();
    }

    Task<ClipboardItem?> GetLatestAsync(CancellationToken cancellationToken);

    Task<ClipboardItem?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return Task.FromResult<ClipboardItem?>(null);
    }

    Task IncrementUseCountAsync(Guid id, CancellationToken cancellationToken);

    async Task IncrementUseCountsAsync(IReadOnlyDictionary<Guid, int> increments, CancellationToken cancellationToken)
    {
        foreach (var (id, count) in increments)
        {
            for (var index = 0; index < count; index++)
            {
                await IncrementUseCountAsync(id, cancellationToken);
            }
        }
    }

    Task<int> DeleteAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken);

    Task SetPinnedAsync(Guid id, bool isPinned, CancellationToken cancellationToken);

    Task SetFavoriteAsync(Guid id, bool isFavorite, CancellationToken cancellationToken);

    Task<bool> IsProcessBlockedAsync(string? processName, CancellationToken cancellationToken);

    Task<int> GetCountAsync(CancellationToken cancellationToken);

    Task<int> ClearAllAsync(CancellationToken cancellationToken);

    Task<int> ClearUnpinnedAsync(CancellationToken cancellationToken);

    Task<int> CleanupAsync(int maxItems, int retentionDays, CancellationToken cancellationToken);

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
}
