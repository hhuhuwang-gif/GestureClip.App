using Xunit;
using Dapper;
using GestureClip.Infrastructure.Database;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;

namespace GestureClip.Tests.Database;

public sealed class SqlMigrationRunnerTests
{
    [Fact]
    public async Task RunAsync_records_successful_migration_once_when_called_repeatedly()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var runner = new SqlMigrationRunner(NullLogger<SqlMigrationRunner>.Instance);
        var migrations = new[]
        {
            new SqlMigration(1, "create_probe", "CREATE TABLE Probe (Id INTEGER PRIMARY KEY);")
        };

        await runner.RunAsync(connection, migrations, CancellationToken.None);
        await runner.RunAsync(connection, migrations, CancellationToken.None);

        var applied = await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM SchemaMigrations WHERE Version = 1;");
        var tables = await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'Probe';");

        Assert.Equal(1, applied);
        Assert.Equal(1, tables);
    }

    [Fact]
    public async Task RunAsync_rolls_back_failed_migration_and_does_not_record_it()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var runner = new SqlMigrationRunner(NullLogger<SqlMigrationRunner>.Instance);
        var migrations = new[]
        {
            new SqlMigration(2, "broken", "CREATE TABLE Broken (Id INTEGER PRIMARY KEY); INSERT INTO MissingTable VALUES (1);")
        };

        await Assert.ThrowsAsync<SqliteException>(() =>
            runner.RunAsync(connection, migrations, CancellationToken.None));

        var applied = await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM SchemaMigrations WHERE Version = 2;");
        var brokenTable = await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'Broken';");

        Assert.Equal(0, applied);
        Assert.Equal(0, brokenTable);
    }

    [Fact]
    public async Task ClipboardPerformanceMigration_creates_last_used_index()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var runner = new SqlMigrationRunner(NullLogger<SqlMigrationRunner>.Instance);

        await runner.RunAsync(connection, new[]
        {
            new SqlMigration(1, "initial", InitialMigration.Sql),
            new SqlMigration(3, "clipboard_performance_indexes", ClipboardPerformanceMigration.Sql)
        }, CancellationToken.None);

        var indexCount = await connection.ExecuteScalarAsync<int>(
            """
SELECT COUNT(*)
FROM sqlite_master
WHERE type = 'index'
  AND name = 'IX_ClipboardItems_LastUsedAt';
""");

        Assert.Equal(1, indexCount);
    }

    [Fact]
    public async Task ClipboardPerformanceV2Migration_creates_filter_indexes()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var runner = new SqlMigrationRunner(NullLogger<SqlMigrationRunner>.Instance);

        await runner.RunAsync(connection, new[]
        {
            new SqlMigration(1, "initial", InitialMigration.Sql),
            new SqlMigration(4, "clipboard_performance_indexes_v2", ClipboardPerformanceV2Migration.Sql)
        }, CancellationToken.None);

        var indexNames = (await connection.QueryAsync<string>(
            """
SELECT name
FROM sqlite_master
WHERE type = 'index'
  AND name IN (
      'IX_ClipboardItems_ContentType_CreatedAt',
      'IX_ClipboardItems_Favorite_CreatedAt'
  );
""")).ToArray();

        Assert.Contains("IX_ClipboardItems_ContentType_CreatedAt", indexNames);
        Assert.Contains("IX_ClipboardItems_Favorite_CreatedAt", indexNames);
    }

    [Fact]
    public async Task ClipboardThumbnailMigration_adds_thumbnail_column()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var runner = new SqlMigrationRunner(NullLogger<SqlMigrationRunner>.Instance);

        await runner.RunAsync(connection, new[]
        {
            new SqlMigration(1, "legacy_clipboard_without_thumbnail", """
CREATE TABLE ClipboardItems (
    Id TEXT PRIMARY KEY,
    ContentType TEXT NOT NULL,
    TextContent TEXT
);
"""),
            new SqlMigration(5, "clipboard_image_thumbnails", ClipboardThumbnailMigration.Sql)
        }, CancellationToken.None);

        var hasThumbnailColumn = await connection.ExecuteScalarAsync<int>(
            """
SELECT COUNT(*)
FROM pragma_table_info('ClipboardItems')
WHERE name = 'ThumbnailContent';
""");

        Assert.Equal(1, hasThumbnailColumn);
    }
}

