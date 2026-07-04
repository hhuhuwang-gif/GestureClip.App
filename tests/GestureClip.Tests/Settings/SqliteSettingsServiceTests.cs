using Dapper;
using GestureClip.Infrastructure.Database;
using GestureClip.Infrastructure.Settings;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GestureClip.Tests.Settings;

public sealed class SqliteSettingsServiceTests
{
    [Fact]
    public async Task Get_uses_cached_value_after_first_database_read()
    {
        using var database = await TestDatabase.CreateAsync();
        await using (var connection = await database.ConnectionFactory.OpenConnectionAsync(CancellationToken.None))
        {
            await connection.ExecuteAsync(
                """
INSERT INTO Settings (Key, Value, ValueType, UpdatedAt)
VALUES ('Probe.Cached', '1', 'int', @UpdatedAt);
""",
                new { UpdatedAt = DateTimeOffset.UtcNow.ToString("O") });
        }

        var service = new SqliteSettingsService(database.ConnectionFactory);

        var first = service.Get("Probe.Cached", 0);
        await using (var connection = await database.ConnectionFactory.OpenConnectionAsync(CancellationToken.None))
        {
            await connection.ExecuteAsync("UPDATE Settings SET Value = '2' WHERE Key = 'Probe.Cached';");
        }

        var second = service.Get("Probe.Cached", 0);

        Assert.Equal(1, first);
        Assert.Equal(1, second);
    }

    [Fact]
    public async Task SetAsync_updates_cached_value()
    {
        using var database = await TestDatabase.CreateAsync();
        var service = new SqliteSettingsService(database.ConnectionFactory);

        await service.SetAsync("Probe.Cached", 3, CancellationToken.None);
        var value = service.Get("Probe.Cached", 0);

        Assert.Equal(3, value);
    }

    private sealed class TestDatabase : IDisposable
    {
        private readonly string _path;

        private TestDatabase(string path)
        {
            _path = path;
            ConnectionFactory = new SqliteConnectionFactory(
                new DatabaseOptions { DatabasePath = path },
                NullLogger<SqliteConnectionFactory>.Instance);
        }

        public ISqliteConnectionFactory ConnectionFactory { get; }

        public static async Task<TestDatabase> CreateAsync()
        {
            var path = Path.Combine(Path.GetTempPath(), $"gestureclip-settings-tests-{Guid.NewGuid():N}.db");
            var database = new TestDatabase(path);
            await using var connection = await database.ConnectionFactory.OpenConnectionAsync(CancellationToken.None);
            var runner = new SqlMigrationRunner(NullLogger<SqlMigrationRunner>.Instance);
            await runner.RunAsync(connection, [new SqlMigration(1, "initial", InitialMigration.Sql)], CancellationToken.None);
            return database;
        }

        public void Dispose()
        {
            try
            {
                File.Delete(_path);
                File.Delete($"{_path}-wal");
                File.Delete($"{_path}-shm");
            }
            catch
            {
            }
        }
    }
}
