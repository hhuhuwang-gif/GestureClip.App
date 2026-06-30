using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace GestureClip.Infrastructure.Database;

public sealed class SqlMigrationRunner
{
    private readonly ILogger<SqlMigrationRunner> _logger;

    public SqlMigrationRunner(ILogger<SqlMigrationRunner> logger)
    {
        _logger = logger;
    }

    public async Task RunAsync(
        SqliteConnection connection,
        IReadOnlyCollection<SqlMigration> migrations,
        CancellationToken cancellationToken)
    {
        await EnsureSchemaMigrationsTableAsync(connection);

        var applied = (await connection.QueryAsync<int>(
            "SELECT Version FROM SchemaMigrations;")).ToHashSet();

        foreach (var migration in migrations.OrderBy(item => item.Version))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (applied.Contains(migration.Version))
            {
                continue;
            }

            await ApplyMigrationAsync(connection, migration);
        }
    }

    private static Task EnsureSchemaMigrationsTableAsync(SqliteConnection connection)
    {
        const string sql = """
CREATE TABLE IF NOT EXISTS SchemaMigrations (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Version INTEGER NOT NULL UNIQUE,
    Name TEXT NOT NULL,
    AppliedAt TEXT NOT NULL
);
""";
        return connection.ExecuteAsync(sql);
    }

    private async Task ApplyMigrationAsync(SqliteConnection connection, SqlMigration migration)
    {
        await using var transaction = await connection.BeginTransactionAsync();

        try
        {
            _logger.LogInformation("Applying migration {MigrationVersion}: {MigrationName}.", migration.Version, migration.Name);

            await connection.ExecuteAsync(migration.Sql, transaction: transaction);
            await connection.ExecuteAsync(
                """
INSERT INTO SchemaMigrations (Version, Name, AppliedAt)
VALUES (@Version, @Name, @AppliedAt);
""",
                new
                {
                    migration.Version,
                    migration.Name,
                    AppliedAt = DateTimeOffset.UtcNow.ToString("O")
                },
                transaction);

            await transaction.CommitAsync();
            _logger.LogInformation("Applied migration {MigrationVersion}.", migration.Version);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Migration {MigrationVersion} failed and was rolled back.", migration.Version);
            throw;
        }
    }
}
