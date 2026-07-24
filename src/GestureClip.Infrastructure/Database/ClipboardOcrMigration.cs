namespace GestureClip.Infrastructure.Database;

public static class ClipboardOcrMigration
{
    public const string Sql = """
ALTER TABLE ClipboardItems ADD COLUMN OcrText TEXT;
""";
}
