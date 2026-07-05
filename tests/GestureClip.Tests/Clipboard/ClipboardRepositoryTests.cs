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
    public async Task ConnectionFactory_sets_busy_timeout_for_clipboard_contention()
    {
        using var database = await TestDatabase.CreateAsync();
        await using var connection = await database.ConnectionFactory.OpenConnectionAsync(CancellationToken.None);

        var busyTimeout = await connection.ExecuteScalarAsync<int>("PRAGMA busy_timeout;");

        Assert.Equal(5000, busyTimeout);
    }

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
    public async Task InsertAsync_ignores_duplicate_hash()
    {
        using var database = await TestDatabase.CreateAsync();
        var repository = new ClipboardRepository(database.ConnectionFactory);
        var first = CreateItem("same", "first", isPinned: false, minutesAgo: 2);
        var duplicate = CreateItem("same", "second", isPinned: false, minutesAgo: 1);

        await repository.InsertAsync(first, CancellationToken.None);
        await repository.InsertAsync(duplicate, CancellationToken.None);

        var results = await repository.SearchAsync("", 10, CancellationToken.None);
        Assert.Single(results);
        Assert.Equal("first", results[0].TextContent);
    }

    [Fact]
    public async Task SearchAsync_with_5000_items_returns_only_requested_limit()
    {
        using var database = await TestDatabase.CreateAsync();
        var repository = new ClipboardRepository(database.ConnectionFactory);
        var now = DateTimeOffset.UtcNow;
        for (var index = 0; index < 5000; index++)
        {
            await repository.InsertAsync(
                CreateItem($"bulk-{index}", $"bulk keyword {index}", isPinned: false, createdAt: now.AddSeconds(-index)),
                CancellationToken.None);
        }

        var results = await repository.SearchAsync("keyword", 50, CancellationToken.None);

        Assert.Equal(50, results.Count);
        Assert.Equal("bulk keyword 0", results[0].TextContent);
        Assert.Equal("bulk keyword 49", results[^1].TextContent);
    }


    [Fact]
    public async Task SearchAsync_with_offset_returns_next_page()
    {
        using var database = await TestDatabase.CreateAsync();
        var repository = new ClipboardRepository(database.ConnectionFactory);
        var now = DateTimeOffset.UtcNow;
        for (var index = 0; index < 75; index++)
        {
            await repository.InsertAsync(
                CreateItem($"paged-{index}", $"paged keyword {index:000}", isPinned: false, createdAt: now.AddSeconds(-index)),
                CancellationToken.None);
        }

        var first = await repository.SearchAsync("keyword", 50, 0, CancellationToken.None);
        var second = await repository.SearchAsync("keyword", 50, 50, CancellationToken.None);

        Assert.Equal(50, first.Count);
        Assert.Equal(25, second.Count);
        Assert.Equal("paged keyword 000", first[0].TextContent);
        Assert.Equal("paged keyword 050", second[0].TextContent);
        Assert.Equal("paged keyword 074", second[^1].TextContent);
    }

    [Fact]
    public async Task SearchAsync_filters_images_before_limit_and_offset()
    {
        using var database = await TestDatabase.CreateAsync();
        var repository = new ClipboardRepository(database.ConnectionFactory);
        var now = DateTimeOffset.UtcNow;
        for (var index = 0; index < 60; index++)
        {
            await repository.InsertAsync(
                CreateItem($"text-{index}", $"text {index:000}", isPinned: false, createdAt: now.AddSeconds(-index)),
                CancellationToken.None);
        }

        for (var index = 0; index < 12; index++)
        {
            await repository.InsertAsync(
                CreateImageItem($"image-{index}", $"full-image-{index}", $"thumb-{index}", minutesAgo: 120 + index),
                CancellationToken.None);
        }

        var first = await repository.SearchAsync("", 10, 0, ClipboardContentFilter.Images, CancellationToken.None);
        var second = await repository.SearchAsync("", 10, 10, ClipboardContentFilter.Images, CancellationToken.None);

        Assert.Equal(10, first.Count);
        Assert.Equal(2, second.Count);
        Assert.All(first.Concat(second), item => Assert.Equal("image/png", item.ContentType));
        Assert.Equal("thumb-0", first[0].ThumbnailContent);
        Assert.Equal("thumb-10", second[0].ThumbnailContent);
    }

    [Fact]
    public async Task SearchAsync_returns_image_thumbnail_without_full_image_content()
    {
        using var database = await TestDatabase.CreateAsync();
        var repository = new ClipboardRepository(database.ConnectionFactory);
        var image = CreateImageItem("image", "full-image-base64", "thumb-base64", minutesAgo: 1);
        await repository.InsertAsync(image, CancellationToken.None);

        var results = await repository.SearchAsync("", 10, CancellationToken.None);
        var full = await repository.GetByIdAsync(image.Id, CancellationToken.None);

        var result = Assert.Single(results);
        Assert.Null(result.TextContent);
        Assert.Equal("thumb-base64", result.ThumbnailContent);
        Assert.Equal("full-image-base64", full!.TextContent);
    }

    [Fact]
    public async Task SearchAsync_does_not_return_full_image_for_legacy_rows_without_thumbnail()
    {
        using var database = await TestDatabase.CreateAsync();
        var repository = new ClipboardRepository(database.ConnectionFactory);
        var image = CreateImageItem("legacy-image", "legacy-full-image-base64", thumbnail: null, minutesAgo: 1);
        await repository.InsertAsync(image, CancellationToken.None);

        var result = Assert.Single(await repository.SearchAsync("", 10, CancellationToken.None));

        Assert.Null(result.TextContent);
        Assert.Null(result.ThumbnailContent);
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
    public async Task IncrementUseCountsAsync_adds_counts_in_one_batch()
    {
        using var database = await TestDatabase.CreateAsync();
        var repository = new ClipboardRepository(database.ConnectionFactory);
        var first = CreateItem("one", "hello", isPinned: false, minutesAgo: 1);
        var second = CreateItem("two", "world", isPinned: false, minutesAgo: 2);
        await repository.InsertAsync(first, CancellationToken.None);
        await repository.InsertAsync(second, CancellationToken.None);

        await repository.IncrementUseCountsAsync(
            new Dictionary<Guid, int>
            {
                [first.Id] = 5,
                [second.Id] = 2
            },
            CancellationToken.None);

        await using var connection = await database.ConnectionFactory.OpenConnectionAsync(CancellationToken.None);
        var rows = (await connection.QueryAsync<(string Id, int UseCount)>(
            "SELECT Id, UseCount FROM ClipboardItems;")).ToDictionary(row => Guid.Parse(row.Id), row => row.UseCount);

        Assert.Equal(5, rows[first.Id]);
        Assert.Equal(2, rows[second.Id]);
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
        await repository.InsertAsync(CreateItem("favorite", "favorite", isPinned: false, minutesAgo: 2) with { IsFavorite = true }, CancellationToken.None);
        await repository.InsertAsync(CreateItem("normal1", "normal1", isPinned: false, minutesAgo: 2), CancellationToken.None);
        await repository.InsertAsync(CreateItem("normal2", "normal2", isPinned: false, minutesAgo: 3), CancellationToken.None);

        var deleted = await repository.ClearUnpinnedAsync(CancellationToken.None);
        var results = await repository.SearchAsync("", 10, CancellationToken.None);

        Assert.Equal(2, deleted);
        Assert.Equal(["pinned", "favorite"], results.Select(item => item.TextContent ?? "").ToArray());
        Assert.True(results[0].IsPinned);
        Assert.True(results[1].IsFavorite);
    }

    [Fact]
    public async Task DeleteAsync_deletes_selected_items_and_returns_deleted_count()
    {
        using var database = await TestDatabase.CreateAsync();
        var repository = new ClipboardRepository(database.ConnectionFactory);
        var first = CreateItem("one", "one", isPinned: false, minutesAgo: 1);
        var second = CreateItem("two", "two", isPinned: false, minutesAgo: 2);
        var third = CreateItem("three", "three", isPinned: false, minutesAgo: 3);
        await repository.InsertAsync(first, CancellationToken.None);
        await repository.InsertAsync(second, CancellationToken.None);
        await repository.InsertAsync(third, CancellationToken.None);

        var deleted = await repository.DeleteAsync([first.Id, third.Id], CancellationToken.None);
        var results = await repository.SearchAsync("", 10, CancellationToken.None);

        Assert.Equal(2, deleted);
        Assert.Equal(["two"], results.Select(item => item.TextContent ?? "").ToArray());
    }

    [Fact]
    public async Task SetPinnedAsync_updates_pin_state()
    {
        using var database = await TestDatabase.CreateAsync();
        var repository = new ClipboardRepository(database.ConnectionFactory);
        var item = CreateItem("one", "one", isPinned: false, minutesAgo: 1);
        await repository.InsertAsync(item, CancellationToken.None);

        await repository.SetPinnedAsync(item.Id, true, CancellationToken.None);
        var pinned = await repository.SearchAsync("", 10, CancellationToken.None);
        await repository.SetPinnedAsync(item.Id, false, CancellationToken.None);
        var unpinned = await repository.SearchAsync("", 10, CancellationToken.None);

        Assert.True(pinned.Single().IsPinned);
        Assert.False(unpinned.Single().IsPinned);
    }

    [Fact]
    public async Task SetFavoriteAsync_updates_favorite_state()
    {
        using var database = await TestDatabase.CreateAsync();
        var repository = new ClipboardRepository(database.ConnectionFactory);
        var item = CreateItem("one", "one", isPinned: false, minutesAgo: 1);
        await repository.InsertAsync(item, CancellationToken.None);

        await repository.SetFavoriteAsync(item.Id, true, CancellationToken.None);
        var favorite = await repository.SearchAsync("", 10, CancellationToken.None);
        await repository.SetFavoriteAsync(item.Id, false, CancellationToken.None);
        var normal = await repository.SearchAsync("", 10, CancellationToken.None);

        Assert.True(favorite.Single().IsFavorite);
        Assert.False(normal.Single().IsFavorite);
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
        await repository.InsertAsync(CreateItem("favorite", "favorite", isPinned: false, minutesAgo: 50) with { IsFavorite = true }, CancellationToken.None);

        var deleted = await repository.CleanupAsync(maxItems: 2, retentionDays: 0, CancellationToken.None);
        var results = await repository.SearchAsync("", 10, CancellationToken.None);

        Assert.Equal(1, deleted);
        Assert.Equal(["pinned", "new", "middle", "favorite"], results.Select(item => item.TextContent ?? "").ToArray());
    }

    [Fact]
    public async Task CleanupAsync_deletes_old_unpinned_items_by_retention_days()
    {
        using var database = await TestDatabase.CreateAsync();
        var repository = new ClipboardRepository(database.ConnectionFactory);
        await repository.InsertAsync(CreateItemDaysAgo("old", "old", isPinned: false, daysAgo: 40), CancellationToken.None);
        await repository.InsertAsync(CreateItemDaysAgo("recent", "recent", isPinned: false, daysAgo: 5), CancellationToken.None);
        await repository.InsertAsync(CreateItemDaysAgo("pinned-old", "pinned-old", isPinned: true, daysAgo: 60), CancellationToken.None);
        await repository.InsertAsync(CreateItemDaysAgo("favorite-old", "favorite-old", isPinned: false, daysAgo: 90) with { IsFavorite = true }, CancellationToken.None);

        var deleted = await repository.CleanupAsync(maxItems: 1000, retentionDays: 30, CancellationToken.None);
        var results = await repository.SearchAsync("", 10, CancellationToken.None);

        Assert.Equal(1, deleted);
        Assert.Equal(["pinned-old", "recent", "favorite-old"], results.Select(item => item.TextContent ?? "").ToArray());
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

    private static ClipboardItem CreateImageItem(string hashSeed, string fullImage, string? thumbnail, int minutesAgo)
    {
        var createdAt = DateTimeOffset.UtcNow.AddMinutes(-minutesAgo);
        return new ClipboardItem(
            Guid.NewGuid(),
            "image/png",
            fullImage,
            "图片",
            $"hash-{hashSeed}",
            null,
            "Test",
            "test.exe",
            false,
            false,
            false,
            0,
            createdAt,
            createdAt,
            null,
            thumbnail);
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
            await runner.RunAsync(connection,
                [
                    new SqlMigration(1, "initial", InitialMigration.Sql),
                    new SqlMigration(5, "clipboard_image_thumbnails", ClipboardThumbnailMigration.Sql)
                ],
                CancellationToken.None);
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
