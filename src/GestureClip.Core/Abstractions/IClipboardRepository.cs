using GestureClip.Core.Clipboard;

namespace GestureClip.Core.Abstractions;

public interface IClipboardRepository
{
    Task<ClipboardItem?> FindByHashAsync(string hash, CancellationToken cancellationToken);

    Task InsertAsync(ClipboardItem item, CancellationToken cancellationToken);

    Task<IReadOnlyList<ClipboardItem>> SearchAsync(string keyword, int limit, CancellationToken cancellationToken);

    Task<ClipboardItem?> GetLatestAsync(CancellationToken cancellationToken);

    Task IncrementUseCountAsync(Guid id, CancellationToken cancellationToken);

    Task<int> DeleteAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken);

    Task SetPinnedAsync(Guid id, bool isPinned, CancellationToken cancellationToken);

    Task SetFavoriteAsync(Guid id, bool isFavorite, CancellationToken cancellationToken);

    Task<bool> IsProcessBlockedAsync(string? processName, CancellationToken cancellationToken);

    Task<int> GetCountAsync(CancellationToken cancellationToken);

    Task<int> ClearAllAsync(CancellationToken cancellationToken);

    Task<int> ClearUnpinnedAsync(CancellationToken cancellationToken);

    Task<int> CleanupAsync(int maxItems, int retentionDays, CancellationToken cancellationToken);
}
