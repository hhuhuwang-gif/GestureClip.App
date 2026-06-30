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

    [Fact]
    public async Task GetCountAsync_returns_clipboard_item_count()
    {
        using var database = await TestDatabase.CreateAsync();
        var repository = new ClipboardRepository(database.ConnectionFactory);
        await repository.InsertAsync(CreateItem("one", "one", isPinned: false, minutesAgo: 1), CancellationToken.None);
        await repository.InsertAsync(CreateItem("two", "two", isPinned: false, minutesAgo: 2), CancellationToken.None);

        var count = await repository.GetCountAsync(CancellationToken.None);

        Assert.Equal(2, count);
    }

    [Fact]
    public async Task ClearAllAsync_deletes_all_items_and_returns_deleted_count()
    {
        using var database = await TestDatabase.CreateAsync();
        var repository = new ClipboardRepository(database.ConnectionFactory);
        await repository.InsertAsync(CreateItem("pinned", "pinned", isPinned: true, minutesAgo: 1), CancellationToken.None);
        await repository.InsertAsync(CreateItem("normal", "normal", isPinned: false, minutesAgo: 2), CancellationToken.None);

        var deleted = await repository.ClearAllAsync(CancellationToken.None);

        Assert.Equal(2, deleted);
        Assert.Equal(0, await repository.GetCountAsync(CancellationToken.None));
    }

    [Fact]
    public async Task ClearUnpinnedAsync_deletes_only_unpinned_items()
    {
        using var database = await TestDatabase.CreateAsync();
        var repository = new ClipboardRepository(database.ConnectionFactory);
        await repository.InsertAsync(CreateItem("pinned", "pinned", isPinned: true, minutesAgo: 1), CancellationToken.None);
        await repository.InsertAsync(CreateItem("normal1", "normal1", isPinned: false, minutesAgo: 2), CancellationToken.None);
        await repository.InsertAsync(CreateItem("normal2", "normal2", isPinned: false, minutesAgo: 3), CancellationToken.None);

        var deleted = await repository.ClearUnpinnedAsync(CancellationToken.None);
        var results = await repository.SearchAsync("", 10, CancellationToken.None);

        Assert.Equal(2, deleted);
        Assert.Single(results);
        Assert.True(results[0].IsPinned);
    }

    [Fact]
    public async Task CleanupAsync_keeps_latest_unpinned_items_by_max_count()
    {
        using var database = await TestDatabase.CreateAsync();
        var repository = new ClipboardRepository(database.ConnectionFactory);
        await repository.InsertAsync(CreateItem("old", "old", isPinned: false, minutesAgo: 30), CancellationToken.None);
        await repository.InsertAsync(CreateItem("middle", "middle", isPinned: false, minutesAgo: 20), CancellationToken.None);
        await repository.InsertAsync(CreateItem("new", "new", isPinned: false, minutesAgo: 10), CancellationToken.None);
        await repository.InsertAsync(CreateItem("pinned", "pinned", isPinned: true, minutesAgo: 40), CancellationToken.None);

        var deleted = await repository.CleanupAsync(maxItems: 2, retentionDays: 0, CancellationToken.None);
        var results = await repository.SearchAsync("", 10, CancellationToken.None);

        Assert.Equal(1, deleted);
        Assert.Equal(["pinned", "new", "middle"], results.Select(item => item.TextContent ?? "").ToArray());
    }

    [Fact]
    public async Task CleanupAsync_deletes_old_unpinned_items_by_retention_days()
    {
        using var database = await TestDatabase.CreateAsync();
        var repository = new ClipboardRepository(database.ConnectionFactory);
        await repository.InsertAsync(CreateItemDaysAgo("old", "old", isPinned: false, daysAgo: 40), CancellationToken.None);
        await repository.InsertAsync(CreateItemDaysAgo("recent", "recent", isPinned: false, daysAgo: 5), CancellationToken.None);
        await repository.InsertAsync(CreateItemDaysAgo("pinned-old", "pinned-old", isPinned: true, daysAgo: 60), CancellationToken.None);

        var deleted = await repository.CleanupAsync(maxItems: 1000, retentionDays: 30, CancellationToken.None);
        var results = await repository.SearchAsync("", 10, CancellationToken.None);

        Assert.Equal(1, deleted);
        Assert.Equal(["pinned-old", "recent"], results.Select(item => item.TextContent ?? "").ToArray());
    }

    [Fact]
    public async Task CleanupAsync_retention_zero_does_not_delete_by_age()
    {
        using var database = await TestDatabase.CreateAsync();
        var repository = new ClipboardRepository(database.ConnectionFactory);
        await repository.InsertAsync(CreateItemDaysAgo("old", "old", isPinned: false, daysAgo: 400), CancellationToken.None);

        var deleted = await repository.CleanupAsync(maxItems: 1000, retentionDays: 0, CancellationToken.None);

        Assert.Equal(0, deleted);
        Assert.Equal(1, await repository.GetCountAsync(CancellationToken.None));
    }

    [Fact]
    public async Task CleanupAsync_invalid_values_fall_back_to_safe_defaults()
    {
        using var database = await TestDatabase.CreateAsync();
        var repository = new ClipboardRepository(database.ConnectionFactory);
        await repository.InsertAsync(CreateItemDaysAgo("old", "old", isPinned: false, daysAgo: 40), CancellationToken.None);
        await repository.InsertAsync(CreateItemDaysAgo("recent", "recent", isPinned: false, daysAgo: 1), CancellationToken.None);

        var deleted = await repository.CleanupAsync(maxItems: -1, retentionDays: -1, CancellationToken.None);

        Assert.Equal(1, deleted);
        Assert.Equal(["recent"], (await repository.SearchAsync("", 10, CancellationToken.None)).Select(item => item.TextContent ?? "").ToArray());
    }

    private static ClipboardItem CreateItem(string hashSeed, string text, bool isPinned, int minutesAgo)
    {
        return CreateItem(hashSeed, text, isPinned, DateTimeOffset.UtcNow.AddMinutes(-minutesAgo));
    }

    private static ClipboardItem CreateItemDaysAgo(string hashSeed, string text, bool isPinned, int daysAgo)
    {
        return CreateItem(hashSeed, text, isPinned, DateTimeOffset.UtcNow.AddDays(-daysAgo));
    }

    private static ClipboardItem CreateItem(string hashSeed, string text, bool isPinned, DateTimeOffset createdAt)
    {
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
