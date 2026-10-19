using Dapper;
using GestureClip.Core.Clipboard;

namespace GestureClip.Features.Clipboard;

public sealed partial class ClipboardRepository
{
    private const int MaximumSearchResults = 200;

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

        var safeLimit = Math.Clamp(limit, 1, MaximumSearchResults);
        var safeOffset = Math.Max(0, offset);
        var normalizedKeyword = keyword.Trim();
        var whereSql = BuildSearchWhereSql(normalizedKeyword, filter);

        var sql = $"""
SELECT
    Id, ContentType,
    CASE WHEN ContentType LIKE 'image/%' THEN NULL ELSE TextContent END AS TextContent,
    ThumbnailContent,
    PreviewText, Hash, PlainTextHash, SourceApp, SourceProcess,
    IsPinned, IsFavorite, IsSensitive, UseCount, CreatedAt, UpdatedAt, LastUsedAt, OcrText
FROM ClipboardItems
{whereSql}
ORDER BY IsPinned DESC, COALESCE(LastUsedAt, CreatedAt) DESC, CreatedAt DESC
LIMIT @Limit OFFSET @Offset;
""";

        var results = await connection.QueryAsync<ClipboardItemRow>(new CommandDefinition(
            sql,
            new
            {
                LikeKeyword = $"%{EscapeLikeValue(normalizedKeyword)}%",
                Limit = safeLimit,
                Offset = safeOffset
            }, cancellationToken: cancellationToken));

        return results.Select(row => row.ToModel()).ToArray();
    }

    private static string BuildSearchWhereSql(string normalizedKeyword, ClipboardContentFilter filter)
    {
        var whereParts = new List<string>();
        var filterSql = ToFilterSql(filter);
        if (filterSql.Length > 0)
        {
            whereParts.Add(filterSql);
        }

        if (normalizedKeyword.Length > 0)
        {
            whereParts.Add("""
((ContentType = 'text' AND (TextContent LIKE @LikeKeyword ESCAPE '\' OR PreviewText LIKE @LikeKeyword ESCAPE '\' OR IFNULL(OcrText,'') LIKE @LikeKeyword ESCAPE '\'))
   OR (ContentType <> 'text' AND (PreviewText LIKE @LikeKeyword ESCAPE '\' OR IFNULL(OcrText,'') LIKE @LikeKeyword ESCAPE '\')))
""");
        }

        return whereParts.Count == 0
            ? ""
            : $"WHERE {string.Join(" AND ", whereParts)}";

    }

    private static string EscapeLikeValue(string value)
    {
        return value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);
    }

    private static string ToFilterSql(ClipboardContentFilter filter)
    {
        return filter switch
        {
            ClipboardContentFilter.Pinned => "IsPinned = 1",
            ClipboardContentFilter.Favorites => "IsFavorite = 1",
            ClipboardContentFilter.Text => "ContentType = 'text'",
            ClipboardContentFilter.Images => "ContentType LIKE 'image/%'",
            ClipboardContentFilter.Links => "(ContentType = 'text' AND (TextContent LIKE '%://%' OR TextContent LIKE 'www.%' OR PreviewText LIKE '%://%' OR PreviewText LIKE 'www.%'))",
            _ => ""
        };
    }
}
