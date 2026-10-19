using Dapper;
using GestureClip.Core.Clipboard;

namespace GestureClip.Features.Clipboard;

public sealed partial class ClipboardRepository
{
    public async Task TouchAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        await TouchAsync(connection, id, DateTimeOffset.UtcNow);
    }

    public async Task IncrementUseCountAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow;

        await connection.ExecuteAsync(
            """
UPDATE ClipboardItems
SET UseCount = UseCount + 1,
    LastUsedAt = @LastUsedAt,
    UpdatedAt = @LastUsedAt
WHERE Id = @Id;
""",
            new
            {
                Id = id.ToString(),
                LastUsedAt = now.ToString("O")
            });
    }

    public async Task IncrementUseCountsAsync(IReadOnlyDictionary<Guid, int> increments, CancellationToken cancellationToken)
    {
        var safeIncrements = increments
            .Where(item => item.Value > 0)
            .ToArray();
        if (safeIncrements.Length == 0)
        {
            return;
        }

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            var now = DateTimeOffset.UtcNow.ToString("O");
            foreach (var (id, count) in safeIncrements)
            {
                await connection.ExecuteAsync(
                    """
UPDATE ClipboardItems
SET UseCount = UseCount + @Count,
    LastUsedAt = @LastUsedAt,
    UpdatedAt = @LastUsedAt
WHERE Id = @Id;
""",
                    new
                    {
                        Id = id.ToString(),
                        Count = count,
                        LastUsedAt = now
                    },
                    transaction);
            }

            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task SetPinnedAsync(Guid id, bool isPinned, CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);

        await connection.ExecuteAsync(
            """
UPDATE ClipboardItems
SET IsPinned = @IsPinned,
    UpdatedAt = @UpdatedAt
WHERE Id = @Id;
""",
            new
            {
                Id = id.ToString(),
                IsPinned = isPinned ? 1 : 0,
                UpdatedAt = DateTimeOffset.UtcNow.ToString("O")
            });
    }

    public async Task SetFavoriteAsync(Guid id, bool isFavorite, CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);

        await connection.ExecuteAsync(
            """
UPDATE ClipboardItems
SET IsFavorite = @IsFavorite,
    UpdatedAt = @UpdatedAt
WHERE Id = @Id;
""",
            new
            {
                Id = id.ToString(),
                IsFavorite = isFavorite ? 1 : 0,
                UpdatedAt = DateTimeOffset.UtcNow.ToString("O")
            });
    }

    public async Task UpdateTextContentAsync(
        Guid id,
        string text,
        string hash,
        string? plainTextHash,
        string preview,
        CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);

        await connection.ExecuteAsync(
            """
UPDATE ClipboardItems
SET TextContent = @TextContent,
    PreviewText = @PreviewText,
    Hash = @Hash,
    PlainTextHash = @PlainTextHash,
    UpdatedAt = @UpdatedAt
WHERE Id = @Id AND ContentType = 'text';
""",
            new
            {
                Id = id.ToString(),
                TextContent = text,
                PreviewText = preview,
                Hash = hash,
                PlainTextHash = plainTextHash,
                UpdatedAt = DateTimeOffset.UtcNow.ToString("O")
            });
    }

    private static async Task TouchAsync(System.Data.IDbConnection connection, Guid id, DateTimeOffset touchedAt)
    {
        await connection.ExecuteAsync(
            """
UPDATE ClipboardItems
SET LastUsedAt = @LastUsedAt,
    UpdatedAt = @LastUsedAt
WHERE Id = @Id;
""",
            new
            {
                Id = id.ToString(),
                LastUsedAt = touchedAt.ToString("O")
            });
    }

    public async Task SetOcrTextAsync(Guid id, string? ocrText, CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(
            """
UPDATE ClipboardItems
SET OcrText = @OcrText,
    UpdatedAt = @UpdatedAt
WHERE Id = @Id;
""",
            new
            {
                Id = id.ToString(),
                OcrText = ocrText,
                UpdatedAt = DateTimeOffset.UtcNow.ToString("O")
            });
    }
}
