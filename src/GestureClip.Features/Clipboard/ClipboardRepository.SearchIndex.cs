using Dapper;
using GestureClip.Core.Clipboard;

namespace GestureClip.Features.Clipboard;

public sealed partial class ClipboardRepository
{
    public async Task<IReadOnlyList<ClipboardSearchEntry>> ReadSearchIndexAsync(
        ClipboardSearchIndexPage page, CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        var filterSql = ToFilterSql(page.Filter);
        var whereSql = filterSql.Length == 0 ? "" : $"WHERE {filterSql}";
        var sql = $"""
SELECT Id, ContentType = 'text' COLLATE NOCASE AS IsText,
    CASE WHEN ContentType LIKE 'image/%' THEN NULL ELSE TextContent END AS TextContent,
    PreviewText, OcrText
FROM ClipboardItems
{whereSql}
ORDER BY IsPinned DESC, COALESCE(LastUsedAt, CreatedAt) DESC, CreatedAt DESC
LIMIT @Limit OFFSET @Offset;
""";
        var rows = await connection.QueryAsync<SearchEntryRow>(new CommandDefinition(sql,
            new { Limit = Math.Clamp(page.Limit, 1, MaximumSearchResults), Offset = Math.Max(0, page.Offset) },
            cancellationToken: cancellationToken));
        return rows.Select(row => new ClipboardSearchEntry(
            Guid.Parse(row.Id), row.IsText, row.TextContent, row.PreviewText, row.OcrText)).ToArray();
    }

    public async Task<IReadOnlyList<ClipboardItem>> GetDisplayItemsAsync(
        IReadOnlyList<Guid> ids, CancellationToken cancellationToken)
    {
        if (ids.Count == 0) return [];
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        var results = new Dictionary<Guid, ClipboardItem>();
        // Batch SQL parameters without limiting the number of matches returned to the caller.
        foreach (var batch in ids.Distinct().Chunk(MaximumSearchResults))
        {
            var rows = await connection.QueryAsync<ClipboardItemRow>(new CommandDefinition("""
SELECT Id, ContentType,
    CASE WHEN ContentType LIKE 'image/%' THEN NULL ELSE TextContent END AS TextContent,
    ThumbnailContent, PreviewText, Hash, PlainTextHash, SourceApp, SourceProcess,
    IsPinned, IsFavorite, IsSensitive, UseCount, CreatedAt, UpdatedAt, LastUsedAt, OcrText
FROM ClipboardItems
WHERE Id IN @Ids;
""", new { Ids = batch.Select(id => id.ToString()).ToArray() }, cancellationToken: cancellationToken));
            foreach (var row in rows)
            {
                var item = row.ToModel();
                results[item.Id] = item;
            }
        }

        return ids.Where(results.ContainsKey).Select(id => results[id]).ToArray();
    }

    private sealed class SearchEntryRow
    {
        public string Id { get; set; } = "";
        public bool IsText { get; set; }
        public string? TextContent { get; set; }
        public string? PreviewText { get; set; }
        public string? OcrText { get; set; }
    }
}
