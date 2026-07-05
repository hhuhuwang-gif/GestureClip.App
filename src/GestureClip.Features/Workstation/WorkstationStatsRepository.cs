using Dapper;
using GestureClip.Core.Abstractions;
using GestureClip.Core.Workstation;
using GestureClip.Infrastructure.Database;

namespace GestureClip.Features.Workstation;

public sealed class WorkstationStatsRepository : IWorkstationStatsRepository
{
    private readonly ISqliteConnectionFactory _connectionFactory;

    public WorkstationStatsRepository(ISqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<WorkstationDailyStats> GetOrCreateAsync(DateOnly date, CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        var key = ToKey(date);
        var row = await connection.QuerySingleOrDefaultAsync<WorkstationStatsRow>(
            """
SELECT Date, CopyCount, PasteCount, GestureCount, EstimatedSavedClicks, OpenClipboardCount, OverworkReminderCount, FishingMinutes, FishingStartedAt
FROM WorkdayStats
WHERE Date = @Date;
""",
            new { Date = key });

        if (row is not null)
        {
            return ToStats(row);
        }

        var stats = new WorkstationDailyStats(date);
        await SaveAsync(stats, cancellationToken);
        return stats;
    }

    public async Task SaveAsync(WorkstationDailyStats stats, CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(
            """
INSERT INTO WorkdayStats
    (Date, CopyCount, PasteCount, GestureCount, EstimatedSavedClicks, OpenClipboardCount, OverworkReminderCount, FishingMinutes, FishingStartedAt)
VALUES
    (@Date, @CopyCount, @PasteCount, @GestureCount, @EstimatedSavedClicks, @OpenClipboardCount, @OverworkReminderCount, @FishingMinutes, @FishingStartedAt)
ON CONFLICT(Date) DO UPDATE SET
    CopyCount = excluded.CopyCount,
    PasteCount = excluded.PasteCount,
    GestureCount = excluded.GestureCount,
    EstimatedSavedClicks = excluded.EstimatedSavedClicks,
    OpenClipboardCount = excluded.OpenClipboardCount,
    OverworkReminderCount = excluded.OverworkReminderCount,
    FishingMinutes = excluded.FishingMinutes,
    FishingStartedAt = excluded.FishingStartedAt;
""",
            new
            {
                Date = ToKey(stats.Date),
                stats.CopyCount,
                stats.PasteCount,
                stats.GestureCount,
                stats.EstimatedSavedClicks,
                stats.OpenClipboardCount,
                stats.OverworkReminderCount,
                stats.FishingMinutes,
                FishingStartedAt = stats.FishingStartedAt?.ToString("O")
            });
    }

    public async Task IncrementCountersAsync(
        DateOnly date,
        int copyDelta,
        int pasteDelta,
        int gestureDelta,
        int savedClicksDelta,
        CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(
            """
INSERT INTO WorkdayStats
    (Date, CopyCount, PasteCount, GestureCount, EstimatedSavedClicks, OpenClipboardCount, OverworkReminderCount, FishingMinutes, FishingStartedAt)
VALUES
    (@Date, @CopyDelta, @PasteDelta, @GestureDelta, @SavedClicksDelta, 0, 0, 0, NULL)
ON CONFLICT(Date) DO UPDATE SET
    CopyCount = CopyCount + excluded.CopyCount,
    PasteCount = PasteCount + excluded.PasteCount,
    GestureCount = GestureCount + excluded.GestureCount,
    EstimatedSavedClicks = EstimatedSavedClicks + excluded.EstimatedSavedClicks;
""",
            new
            {
                Date = ToKey(date),
                CopyDelta = copyDelta,
                PasteDelta = pasteDelta,
                GestureDelta = gestureDelta,
                SavedClicksDelta = savedClicksDelta
            });
    }

    public async Task IncrementHubCountersAsync(
        DateOnly date,
        int openClipboardDelta,
        int overworkReminderDelta,
        CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(
            """
INSERT INTO WorkdayStats
    (Date, CopyCount, PasteCount, GestureCount, EstimatedSavedClicks, OpenClipboardCount, OverworkReminderCount, FishingMinutes, FishingStartedAt)
VALUES
    (@Date, 0, 0, 0, 0, @OpenClipboardDelta, @OverworkReminderDelta, 0, NULL)
ON CONFLICT(Date) DO UPDATE SET
    OpenClipboardCount = OpenClipboardCount + excluded.OpenClipboardCount,
    OverworkReminderCount = OverworkReminderCount + excluded.OverworkReminderCount;
""",
            new
            {
                Date = ToKey(date),
                OpenClipboardDelta = openClipboardDelta,
                OverworkReminderDelta = overworkReminderDelta
            });
    }

    public Task ResetAsync(DateOnly date, CancellationToken cancellationToken)
    {
        return SaveAsync(new WorkstationDailyStats(date), cancellationToken);
    }

    private static string ToKey(DateOnly date) => date.ToString("yyyy-MM-dd");

    private static WorkstationDailyStats ToStats(WorkstationStatsRow row)
    {
        return new WorkstationDailyStats(
            DateOnly.Parse(row.Date),
            row.CopyCount,
            row.PasteCount,
            row.GestureCount,
            row.EstimatedSavedClicks,
            row.FishingMinutes,
            string.IsNullOrWhiteSpace(row.FishingStartedAt)
                ? null
                : DateTimeOffset.Parse(row.FishingStartedAt),
            row.OpenClipboardCount,
            row.OverworkReminderCount);
    }

    private sealed class WorkstationStatsRow
    {
        public string Date { get; init; } = "";

        public int CopyCount { get; init; }

        public int PasteCount { get; init; }

        public int GestureCount { get; init; }

        public int EstimatedSavedClicks { get; init; }

        public int OpenClipboardCount { get; init; }

        public int OverworkReminderCount { get; init; }

        public int FishingMinutes { get; init; }

        public string? FishingStartedAt { get; init; }
    }
}
