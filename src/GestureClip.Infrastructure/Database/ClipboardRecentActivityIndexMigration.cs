namespace GestureClip.Infrastructure.Database;

public static class ClipboardRecentActivityIndexMigration
{
    public const string Sql = """
CREATE INDEX IF NOT EXISTS IX_ClipboardItems_Pinned_Activity_CreatedAt
ON ClipboardItems (
    IsPinned DESC,
    COALESCE(LastUsedAt, CreatedAt) DESC,
    CreatedAt DESC
);
""";
}
