using GestureClip.Core.Workstation;

namespace GestureClip.Core.Abstractions;

public interface IWorkstationStatsRepository
{
    Task<WorkstationDailyStats> GetOrCreateAsync(DateOnly date, CancellationToken cancellationToken);

    Task SaveAsync(WorkstationDailyStats stats, CancellationToken cancellationToken);

    Task IncrementCountersAsync(
        DateOnly date,
        int copyDelta,
        int pasteDelta,
        int gestureDelta,
        int savedClicksDelta,
        CancellationToken cancellationToken);

    async Task IncrementHubCountersAsync(
        DateOnly date,
        int openClipboardDelta,
        int overworkReminderDelta,
        CancellationToken cancellationToken)
    {
        var stats = await GetOrCreateAsync(date, cancellationToken);
        await SaveAsync(stats with
        {
            OpenClipboardCount = stats.OpenClipboardCount + openClipboardDelta,
            OverworkReminderCount = stats.OverworkReminderCount + overworkReminderDelta
        }, cancellationToken);
    }

    Task ResetAsync(DateOnly date, CancellationToken cancellationToken);

    /// <summary>Inclusive date range, one entry per day; missing days are zero-filled, nothing is written.</summary>
    async Task<IReadOnlyList<WorkstationDailyStats>> GetRangeAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        var results = new List<WorkstationDailyStats>();
        for (var date = from; date <= to; date = date.AddDays(1))
        {
            results.Add(await GetOrCreateAsync(date, cancellationToken));
        }

        return results;
    }
}
