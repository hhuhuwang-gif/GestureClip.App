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
    Id, ContentType, TextContent, PreviewText, Hash, PlainTextHash, SourceApp, SourceProcess,
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
    IsPinned, IsFavorite, IsSensitive, UseCount, CreatedAt, UpdatedAt, LastUsedAt
)
VALUES (
    @Id, @ContentType, @TextContent, @PreviewText, @Hash, @PlainTextHash, @SourceApp, @SourceProcess,
    @IsPinned, @IsFavorite, @IsSensitive, @UseCount, @CreatedAt, @UpdatedAt, @LastUsedAt
);
""",
            ToRow(item));
    }

    public async Task<IReadOnlyList<ClipboardItem>> SearchAsync(string keyword, int limit, CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);

        var safeLimit = Math.Clamp(limit, 1, 200);
        var normalizedKeyword = keyword.Trim();

        var results = await connection.QueryAsync<ClipboardItemRow>(
            """
SELECT
    Id, ContentType, TextContent, PreviewText, Hash, PlainTextHash, SourceApp, SourceProcess,
    IsPinned, IsFavorite, IsSensitive, UseCount, CreatedAt, UpdatedAt, LastUsedAt
FROM ClipboardItems
WHERE ContentType = 'text'
  AND (@Keyword = '' OR TextContent LIKE @LikeKeyword OR PreviewText LIKE @LikeKeyword)
ORDER BY IsPinned DESC, CreatedAt DESC
LIMIT @Limit;
""",
            new
            {
                Keyword = normalizedKeyword,
                LikeKeyword = $"%{EscapeLikeValue(normalizedKeyword)}%",
                Limit = safeLimit
            });

        return results.Select(row => row.ToModel()).ToArray();
    }

    public async Task<ClipboardItem?> GetLatestAsync(CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);

        var row = await connection.QuerySingleOrDefaultAsync<ClipboardItemRow>(
            """
SELECT
    Id, ContentType, TextContent, PreviewText, Hash, PlainTextHash, SourceApp, SourceProcess,
    IsPinned, IsFavorite, IsSensitive, UseCount, CreatedAt, UpdatedAt, LastUsedAt
FROM ClipboardItems
WHERE ContentType = 'text'
ORDER BY CreatedAt DESC
LIMIT 1;
""");

        return row?.ToModel();
    }

    public async Task IncrementUseCountAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);

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
                LastUsedAt = DateTimeOffset.UtcNow.ToString("O")
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

    private static object ToRow(ClipboardItem item)
    {
        return new
        {
            Id = item.Id.ToString(),
            item.ContentType,
            item.TextContent,
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

    private static string EscapeLikeValue(string value)
    {
        return value.Replace("[", "[[]", StringComparison.Ordinal)
            .Replace("%", "[%]", StringComparison.Ordinal)
            .Replace("_", "[_]", StringComparison.Ordinal);
    }

    private sealed class ClipboardItemRow
    {
        public string Id { get; set; } = "";
        public string ContentType { get; set; } = "";
        public string? TextContent { get; set; }
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
                string.IsNullOrWhiteSpace(LastUsedAt) ? null : DateTimeOffset.Parse(LastUsedAt));
        }
    }
}
