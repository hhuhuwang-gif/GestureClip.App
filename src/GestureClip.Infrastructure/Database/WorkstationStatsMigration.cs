namespace GestureClip.Infrastructure.Database;

public static class WorkstationStatsMigration
{
    public const string Sql = """
CREATE TABLE IF NOT EXISTS WorkdayStats (
    Date TEXT PRIMARY KEY,
    CopyCount INTEGER NOT NULL DEFAULT 0,
    PasteCount INTEGER NOT NULL DEFAULT 0,
    GestureCount INTEGER NOT NULL DEFAULT 0,
    EstimatedSavedClicks INTEGER NOT NULL DEFAULT 0,
    FishingMinutes INTEGER NOT NULL DEFAULT 0,
    FishingStartedAt TEXT
);
""";
}
