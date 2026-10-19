using Dapper;
using GestureClip.Core.Clipboard;

namespace GestureClip.Features.Clipboard;

public sealed partial class ClipboardRepository
{
    private const int DefaultMaxItems = 1000;
    private const int DefaultRetentionDays = 30;

    public async Task<int> DeleteAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken)
    {
        var safeIds = ids.Distinct().Select(id => id.ToString()).ToArray();
        if (safeIds.Length == 0)
        {
            return 0;
        }

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            var deleted = await connection.ExecuteAsync(
                """
DELETE FROM ClipboardItems
WHERE Id IN @Ids;
""",
                new { Ids = safeIds },
                transaction);
            await transaction.CommitAsync(cancellationToken);
            return deleted;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<int> ClearAllAsync(CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            var deleted = await connection.ExecuteAsync(
                "DELETE FROM ClipboardItems;",
                transaction: transaction);
            await transaction.CommitAsync(cancellationToken);
            return deleted;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<int> ClearUnpinnedAsync(CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            var deleted = await connection.ExecuteAsync(
                "DELETE FROM ClipboardItems WHERE IsPinned = 0 AND IsFavorite = 0;",
                transaction: transaction);
            await transaction.CommitAsync(cancellationToken);
            return deleted;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<int> CleanupAsync(int maxItems, int retentionDays, CancellationToken cancellationToken)
    {
        var safeMaxItems = maxItems > 0 ? maxItems : DefaultMaxItems;
        var safeRetentionDays = retentionDays >= 0 ? retentionDays : DefaultRetentionDays;
        var deleted = 0;

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            if (safeRetentionDays > 0)
            {
                var cutoff = DateTimeOffset.UtcNow.AddDays(-safeRetentionDays).ToString("O");
                deleted += await connection.ExecuteAsync(
                    """
DELETE FROM ClipboardItems
WHERE IsPinned = 0
  AND IsFavorite = 0
  AND CreatedAt < @Cutoff;
""",
                    new { Cutoff = cutoff },
                    transaction);
            }

            deleted += await connection.ExecuteAsync(
                """
DELETE FROM ClipboardItems
WHERE IsPinned = 0
  AND IsFavorite = 0
  AND Id NOT IN (
      SELECT Id
      FROM ClipboardItems
      WHERE IsPinned = 0
        AND IsFavorite = 0
      ORDER BY CreatedAt DESC
      LIMIT @Limit
  );
""",
                new { Limit = safeMaxItems },
                transaction);

            await transaction.CommitAsync(cancellationToken);
            return deleted;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}
