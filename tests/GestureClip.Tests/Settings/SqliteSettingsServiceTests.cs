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

    [Fact]
    public async Task SetAsync_opens_database_off_the_calling_thread()
    {
        using var database = await TestDatabase.CreateAsync();
        var factory = new RecordingConnectionFactory(database.ConnectionFactory);
        var service = new SqliteSettingsService(factory);
        var invoked = new TaskCompletionSource<Task>(TaskCreationOptions.RunContinuationsAsynchronously);
        var callerThreadId = 0;
        var caller = new Thread(() =>
        {
            callerThreadId = Environment.CurrentManagedThreadId;
            invoked.SetResult(service.SetAsync("Probe.Worker", 7, CancellationToken.None));
        });
        caller.Start();
        await await invoked.Task;

        Assert.NotEqual(callerThreadId, factory.OpenThreadId);
        Assert.Equal(7, service.Get("Probe.Worker", 0));
    }

    [Fact]
    public async Task Concurrent_saves_keep_latest_requested_value_in_database_and_cache()
    {
        using var database = await TestDatabase.CreateAsync();
        var service = new SqliteSettingsService(database.ConnectionFactory);
        const int saveCount = 30;
        var saves = Enumerable.Range(1, saveCount)
            .Select(value => service.SetAsync("Probe.Ordered", value, CancellationToken.None))
            .ToArray();
        await Task.WhenAll(saves);

        Assert.Equal(saveCount, service.Get("Probe.Ordered", 0));
        var reloaded = new SqliteSettingsService(database.ConnectionFactory);
        Assert.Equal(saveCount, reloaded.Get("Probe.Ordered", 0));
    }

    private sealed class RecordingConnectionFactory(ISqliteConnectionFactory inner) : ISqliteConnectionFactory
    {
        public int OpenThreadId { get; private set; }

        public Task<Microsoft.Data.Sqlite.SqliteConnection> OpenConnectionAsync(CancellationToken cancellationToken)
        {
            OpenThreadId = Environment.CurrentManagedThreadId;
            return inner.OpenConnectionAsync(cancellationToken);
        }
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
