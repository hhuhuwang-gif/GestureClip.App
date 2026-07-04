namespace GestureClip.Infrastructure.Database;

public static class ClipboardPerformanceMigration
{
    public const string Sql = """
CREATE INDEX IF NOT EXISTS IX_ClipboardItems_LastUsedAt
ON ClipboardItems (LastUsedAt DESC);
""";
}
