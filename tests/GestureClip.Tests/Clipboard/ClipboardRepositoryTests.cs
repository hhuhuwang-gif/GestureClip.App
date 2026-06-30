using Dapper;
using GestureClip.Core.Clipboard;
using GestureClip.Features.Clipboard;
using GestureClip.Infrastructure.Database;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GestureClip.Tests.Clipboard;

public sealed class ClipboardRepositoryTests
{
    [Fact]
    public async Task InsertAsync_and_SearchAsync_return_pinned_items_first_then_newest()
    {
        using var database = await TestDatabase.CreateAsync();
        var repository = new ClipboardRepository(database.ConnectionFactory);

        await repository.InsertAsync(CreateItem("older", "alpha older", isPinned: false, minutesAgo: 20), CancellationToken.None);
        await repository.InsertAsync(CreateItem("pinned", "alpha pinned", isPinned: true, minutesAgo: 10), CancellationToken.None);
        await repository.InsertAsync(CreateItem("newer", "alpha newer", isPinned: false, minutesAgo: 1), CancellationToken.None);

        var results = await repository.SearchAsync("alpha", 10, CancellationToken.None);

        Assert.Equal(
            ["alpha pinned", "alpha newer", "alpha older"],
            results.Select(item => item.TextContent ?? "").ToArray());
    }

    [Fact]
    public async Task IncrementUseCountAsync_updates_use_count_and_last_used_at()
    {
        using var database = await TestDatabase.CreateAsync();
        var repository = new ClipboardRepository(database.ConnectionFactory);
        var item = CreateItem("one", "hello", isPinned: false, minutesAgo: 1);
        await repository.InsertAsync(item, CancellationToken.None);

        await repository.IncrementUseCountAsync(item.Id, CancellationToken.None);

        await using var connection = await database.ConnectionFactory.OpenConnectionAsync(CancellationToken.None);
        var stored = await connection.QuerySingleAsync<(int UseCount, string? LastUsedAt)>(
            "SELECT UseCount, LastUsedAt FROM ClipboardItems WHERE Id = @Id;",
            new { Id = item.Id.ToString() });

        Assert.Equal(1, stored.UseCount);
        Assert.False(string.IsNullOrWhiteSpace(stored.LastUsedAt));
    }

    private static ClipboardItem CreateItem(string hashSeed, string text, bool isPinned, int minutesAgo)
    {
        var createdAt = DateTimeOffset.UtcNow.AddMinutes(-minutesAgo);

        return new ClipboardItem(
            Guid.NewGuid(),
            "text",
            text,
            text,
            $"hash-{hashSeed}",
            $"plain-{hashSeed}",
            "Test",
            "test.exe",
            isPinned,
            false,
            false,
            0,
            createdAt,
            createdAt,
            null);
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
            var path = Path.Combine(Path.GetTempPath(), $"gestureclip-tests-{Guid.NewGuid():N}.db");
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
