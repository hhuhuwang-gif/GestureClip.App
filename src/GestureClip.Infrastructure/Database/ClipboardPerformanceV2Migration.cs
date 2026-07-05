namespace GestureClip.Infrastructure.Database;

public static class ClipboardPerformanceV2Migration
{
    public const string Sql = """
CREATE INDEX IF NOT EXISTS IX_ClipboardItems_ContentType_CreatedAt
ON ClipboardItems (ContentType, CreatedAt DESC);

CREATE INDEX IF NOT EXISTS IX_ClipboardItems_Favorite_CreatedAt
ON ClipboardItems (IsFavorite DESC, CreatedAt DESC);
""";
}
