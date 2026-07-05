using Dapper;
using Microsoft.Data.Sqlite;

namespace GestureClip.Infrastructure.Database;

public static class WorkstationHubMigration
{
    public const string Sql = """
SELECT 1;
""";

    public static async Task EnsureAsync(SqliteConnection connection)
    {
        var columns = (await connection.QueryAsync<string>("SELECT name FROM pragma_table_info('WorkdayStats');"))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (!columns.Contains("OpenClipboardCount"))
        {
            await connection.ExecuteAsync("ALTER TABLE WorkdayStats ADD COLUMN OpenClipboardCount INTEGER NOT NULL DEFAULT 0;");
        }

        if (!columns.Contains("OverworkReminderCount"))
        {
            await connection.ExecuteAsync("ALTER TABLE WorkdayStats ADD COLUMN OverworkReminderCount INTEGER NOT NULL DEFAULT 0;");
        }
    }
}
