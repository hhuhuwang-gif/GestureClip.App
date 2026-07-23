using GestureClip.Core.Privacy;

namespace GestureClip.Core.Abstractions;

public interface IAppSmartPasteRuleService
{
    Task<IReadOnlyList<AppSmartPasteRule>> GetAllAsync(CancellationToken cancellationToken = default);

    Task SetAsync(string processName, string strategy, string? note = null, CancellationToken cancellationToken = default);

    Task DeleteAsync(string processName, CancellationToken cancellationToken = default);

    /// <summary>Returns null when no override is configured for the process.</summary>
    string? TryGetStrategy(string? processName);

    Task RefreshAsync(CancellationToken cancellationToken = default);
}
