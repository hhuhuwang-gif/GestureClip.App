using GestureClip.Core.Clipboard;

namespace GestureClip.Core.Abstractions;

public interface IClipboardService
{
    bool IsCaptureEnabled { get; }

    DateTimeOffset? SuppressCaptureUntil { get; }

    Task StartAsync(CancellationToken cancellationToken);

    Task StopAsync(CancellationToken cancellationToken);

    Task SetCaptureEnabledAsync(bool enabled, CancellationToken cancellationToken);

    void SuppressCaptureFor(TimeSpan duration);

    Task CaptureTextAsync(ClipboardCapture capture, CancellationToken cancellationToken);

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

    Task PasteAsync(ClipboardItem item, PasteOptions options, CancellationToken cancellationToken);

    Task CopyItemsAsync(IReadOnlyList<ClipboardItem> items, CancellationToken cancellationToken);

    Task<int> DeleteItemsAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken);

    Task SetPinnedAsync(Guid id, bool isPinned, CancellationToken cancellationToken);

    Task SetFavoriteAsync(Guid id, bool isFavorite, CancellationToken cancellationToken);

    private static bool MatchesFilter(ClipboardItem item, ClipboardContentFilter filter)
    {
        return filter switch
        {
            ClipboardContentFilter.Pinned => item.IsPinned,
            ClipboardContentFilter.Favorites => item.IsFavorite,
            ClipboardContentFilter.Text => string.Equals(item.ContentType, "text", StringComparison.OrdinalIgnoreCase),
            ClipboardContentFilter.Images => string.Equals(item.ContentType, "image/png", StringComparison.OrdinalIgnoreCase),
            _ => true
        };
    }
}
