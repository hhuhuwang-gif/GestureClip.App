using Dapper;
using GestureClip.Core.Abstractions;
using GestureClip.Core.Clipboard;
using GestureClip.Infrastructure.Database;

namespace GestureClip.Features.Clipboard;

public sealed class ClipboardRepository : IClipboardRepository
{
    private readonly ISqliteConnectionFactory _connectionFactory;

    public ClipboardRepository(ISqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<ClipboardItem?> FindByHashAsync(string hash, CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);

        var row = await connection.QuerySingleOrDefaultAsync<ClipboardItemRow>(
            """
SELECT
    Id, ContentType, TextContent, ThumbnailContent, PreviewText, Hash, PlainTextHash, SourceApp, SourceProcess,
    IsPinned, IsFavorite, IsSensitive, UseCount, CreatedAt, UpdatedAt, LastUsedAt
FROM ClipboardItems
WHERE Hash = @Hash
LIMIT 1;
""",
            new { Hash = hash });

        return row?.ToModel();
    }

    public async Task InsertAsync(ClipboardItem item, CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);

        await connection.ExecuteAsync(
            """
INSERT INTO ClipboardItems (
    Id, ContentType, TextContent, PreviewText, Hash, PlainTextHash, SourceApp, SourceProcess,
    ThumbnailContent, IsPinned, IsFavorite, IsSensitive, UseCount, CreatedAt, UpdatedAt, LastUsedAt
)
SELECT
    @Id, @ContentType, @TextContent, @PreviewText, @Hash, @PlainTextHash, @SourceApp, @SourceProcess,
    @ThumbnailContent, @IsPinned, @IsFavorite, @IsSensitive, @UseCount, @CreatedAt, @UpdatedAt, @LastUsedAt
WHERE NOT EXISTS (
    SELECT 1
    FROM ClipboardItems
    WHERE Hash = @Hash
    LIMIT 1
);
""",
            ToRow(item));
    }

    public async Task<IReadOnlyList<ClipboardItem>> SearchAsync(string keyword, int limit, CancellationToken cancellationToken)
    {
        return await SearchAsync(keyword, limit, 0, cancellationToken);
    }

    public async Task<IReadOnlyList<ClipboardItem>> SearchAsync(string keyword, int limit, int offset, CancellationToken cancellationToken)
    {
        return await SearchAsync(keyword, limit, offset, ClipboardContentFilter.All, cancellationToken);
    }

    public async Task<IReadOnlyList<ClipboardItem>> SearchAsync(
        string keyword,
        int limit,
        int offset,
        ClipboardContentFilter filter,
        CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);

        var safeLimit = Math.Clamp(limit, 1, 200);
        var safeOffset = Math.Max(0, offset);
        var normalizedKeyword = keyword.Trim();
        var whereParts = new List<string>();
        var filterSql = ToFilterSql(filter);
        if (filterSql.Length > 0)
        {
            whereParts.Add(filterSql);
        }

        if (normalizedKeyword.Length > 0)
        {
            whereParts.Add("""
((ContentType = 'text' AND (TextContent LIKE @LikeKeyword OR PreviewText LIKE @LikeKeyword))
   OR (ContentType <> 'text' AND PreviewText LIKE @LikeKeyword))
""");
        }

        var whereSql = whereParts.Count == 0
            ? ""
            : $"WHERE {string.Join(" AND ", whereParts)}";

        var sql = $"""
SELECT
    Id, ContentType,
    CASE WHEN ContentType LIKE 'image/%' THEN NULL ELSE TextContent END AS TextContent,
    CASE
        WHEN ContentType LIKE 'image/%' THEN ThumbnailContent
        ELSE ThumbnailContent
    END AS ThumbnailContent,
    PreviewText, Hash, PlainTextHash, SourceApp, SourceProcess,
    IsPinned, IsFavorite, IsSensitive, UseCount, CreatedAt, UpdatedAt, LastUsedAt
FROM ClipboardItems
{whereSql}
ORDER BY IsPinned DESC, COALESCE(LastUsedAt, CreatedAt) DESC, CreatedAt DESC
LIMIT @Limit OFFSET @Offset;
""";

        var results = await connection.QueryAsync<ClipboardItemRow>(
            sql,
            new
            {
                LikeKeyword = $"%{EscapeLikeValue(normalizedKeyword)}%",
                Limit = safeLimit,
                Offset = safeOffset
            });

        return results.Select(row => row.ToModel()).ToArray();
    }

    public async Task<ClipboardItem?> GetLatestAsync(CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);

        var row = await connection.QuerySingleOrDefaultAsync<ClipboardItemRow>(
            """
SELECT
    Id, ContentType, TextContent, ThumbnailContent, PreviewText, Hash, PlainTextHash, SourceApp, SourceProcess,
    IsPinned, IsFavorite, IsSensitive, UseCount, CreatedAt, UpdatedAt, LastUsedAt
FROM ClipboardItems
WHERE ContentType = 'text'
ORDER BY CreatedAt DESC
LIMIT 1;
""");

        return row?.ToModel();
    }

    public async Task<ClipboardItem?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);

        var row = await connection.QuerySingleOrDefaultAsync<ClipboardItemRow>(
            """
SELECT
    Id, ContentType, TextContent, ThumbnailContent, PreviewText, Hash, PlainTextHash, SourceApp, SourceProcess,
    IsPinned, IsFavorite, IsSensitive, UseCount, CreatedAt, UpdatedAt, LastUsedAt
FROM ClipboardItems
WHERE Id = @Id
LIMIT 1;
""",
            new { Id = id.ToString() });

        return row?.ToModel();
    }

    public async Task TouchAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await TouchAsync(connection, id, DateTimeOffset.UtcNow, cancellationToken);
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

    public async Task<bool> IsProcessBlockedAsync(string? processName, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(processName))
        {
            return false;
        }

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        var count = await connection.ExecuteScalarAsync<int>(
            """
SELECT COUNT(*)
FROM AppBlacklist
WHERE BlockClipboard = 1
  AND lower(ProcessName) = lower(@ProcessName);
""",
            new { ProcessName = processName });

        return count > 0;
    }

    public async Task<int> GetCountAsync(CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM ClipboardItems;");
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
        var safeMaxItems = maxItems > 0 ? maxItems : 1000;
        var safeRetentionDays = retentionDays >= 0 ? retentionDays : 30;
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

    private static object ToRow(ClipboardItem item)
    {
        return new
        {
            Id = item.Id.ToString(),
            item.ContentType,
            item.TextContent,
            item.ThumbnailContent,
            item.PreviewText,
            item.Hash,
            item.PlainTextHash,
            item.SourceApp,
            item.SourceProcess,
            IsPinned = item.IsPinned ? 1 : 0,
            IsFavorite = item.IsFavorite ? 1 : 0,
            IsSensitive = item.IsSensitive ? 1 : 0,
            item.UseCount,
            CreatedAt = item.CreatedAt.ToString("O"),
            UpdatedAt = item.UpdatedAt.ToString("O"),
            LastUsedAt = item.LastUsedAt?.ToString("O")
        };
    }

    private static async Task TouchAsync(System.Data.IDbConnection connection, Guid id, DateTimeOffset touchedAt, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
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

    private static string EscapeLikeValue(string value)
    {
        return value.Replace("[", "[[]", StringComparison.Ordinal)
            .Replace("%", "[%]", StringComparison.Ordinal)
            .Replace("_", "[_]", StringComparison.Ordinal);
    }

    private static string ToFilterSql(ClipboardContentFilter filter)
    {
        return filter switch
        {
            ClipboardContentFilter.Pinned => "IsPinned = 1",
            ClipboardContentFilter.Favorites => "IsFavorite = 1",
            ClipboardContentFilter.Text => "ContentType = 'text'",
            ClipboardContentFilter.Images => "ContentType LIKE 'image/%'",
            _ => ""
        };
    }

    private sealed class ClipboardItemRow
    {
        public string Id { get; set; } = "";
        public string ContentType { get; set; } = "";
        public string? TextContent { get; set; }
        public string? ThumbnailContent { get; set; }
        public string? PreviewText { get; set; }
        public string Hash { get; set; } = "";
        public string? PlainTextHash { get; set; }
        public string? SourceApp { get; set; }
        public string? SourceProcess { get; set; }
        public int IsPinned { get; set; }
        public int IsFavorite { get; set; }
        public int IsSensitive { get; set; }
        public int UseCount { get; set; }
        public string CreatedAt { get; set; } = "";
        public string UpdatedAt { get; set; } = "";
        public string? LastUsedAt { get; set; }

        public ClipboardItem ToModel()
        {
            return new ClipboardItem(
                Guid.Parse(Id),
                ContentType,
                TextContent,
                PreviewText,
                Hash,
                PlainTextHash,
                SourceApp,
                SourceProcess,
                IsPinned == 1,
                IsFavorite == 1,
                IsSensitive == 1,
                UseCount,
                DateTimeOffset.Parse(CreatedAt),
                DateTimeOffset.Parse(UpdatedAt),
                string.IsNullOrWhiteSpace(LastUsedAt) ? null : DateTimeOffset.Parse(LastUsedAt),
                ThumbnailContent);
        }
    }
}
