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
}

