using GestureClip.Core.Clipboard;

namespace GestureClip.Core.Abstractions;

public interface IClipboardRepository
{
    Task<ClipboardItem?> FindByHashAsync(string hash, CancellationToken cancellationToken);

    Task InsertAsync(ClipboardItem item, CancellationToken cancellationToken);

    Task<IReadOnlyList<ClipboardItem>> SearchAsync(string keyword, int limit, CancellationToken cancellationToken);

    Task<ClipboardItem?> GetLatestAsync(CancellationToken cancellationToken);

    Task IncrementUseCountAsync(Guid id, CancellationToken cancellationToken);

    Task<bool> IsProcessBlockedAsync(string? processName, CancellationToken cancellationToken);
}
