namespace GestureClip.Infrastructure.Database;

public static class ClipboardThumbnailMigration
{
    public const string Sql = """
ALTER TABLE ClipboardItems ADD COLUMN ThumbnailContent TEXT;
""";
}
