using GestureClip.Core.Privacy;

namespace GestureClip.Core.Abstractions;

public interface IAppBlacklistService
{
    Task<IReadOnlyList<AppBlacklistItem>> GetAllAsync(CancellationToken cancellationToken);

    Task AddAsync(string processName, bool blockClipboard, bool blockGesture, CancellationToken cancellationToken);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken);

    Task UpdateAsync(Guid id, bool blockClipboard, bool blockGesture, CancellationToken cancellationToken);

    Task<bool> IsClipboardBlockedAsync(string? processName, CancellationToken cancellationToken);

    Task<bool> IsGestureBlockedAsync(string? processName, CancellationToken cancellationToken);

    Task RefreshAsync(CancellationToken cancellationToken);

    bool IsGestureBlockedCached(string? processName);
}
