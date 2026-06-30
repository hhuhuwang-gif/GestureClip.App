using Dapper;
using GestureClip.Features.Privacy;
using GestureClip.Infrastructure.Database;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GestureClip.Tests.Privacy;

public sealed class AppBlacklistServiceTests
{
    [Fact]
    public async Task AddAsync_normalizes_process_name_and_prevents_case_insensitive_duplicates()
    {
        using var database = await TestDatabase.CreateAsync();
        var service = new AppBlacklistService(database.ConnectionFactory, NullLogger<AppBlacklistService>.Instance);

        await service.AddAsync(" notepad ", blockClipboard: true, blockGesture: true, CancellationToken.None);
        await service.AddAsync("NOTEPAD.EXE", blockClipboard: false, blockGesture: false, CancellationToken.None);

        var items = await service.GetAllAsync(CancellationToken.None);

        Assert.Single(items);
        Assert.Equal("notepad.exe", items[0].ProcessName);
        Assert.True(await service.IsClipboardBlockedAsync("Notepad.exe", CancellationToken.None));
        Assert.True(service.IsGestureBlockedCached("NOTEPAD.EXE"));
    }

    [Fact]
    public async Task AddAsync_rejects_empty_process_name()
    {
        using var database = await TestDatabase.CreateAsync();
        var service = new AppBlacklistService(database.ConnectionFactory, NullLogger<AppBlacklistService>.Instance);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.AddAsync("   ", blockClipboard: true, blockGesture: true, CancellationToken.None));
    }

    [Fact]
    public async Task UpdateAsync_and_DeleteAsync_change_matching_results()
    {
        using var database = await TestDatabase.CreateAsync();
        var service = new AppBlacklistService(database.ConnectionFactory, NullLogger<AppBlacklistService>.Instance);

        await service.AddAsync("code.exe", blockClipboard: true, blockGesture: false, CancellationToken.None);
        var item = (await service.GetAllAsync(CancellationToken.None)).Single();

        await service.UpdateAsync(item.Id, blockClipboard: false, blockGesture: true, CancellationToken.None);

        Assert.False(await service.IsClipboardBlockedAsync("code.exe", CancellationToken.None));
        Assert.True(await service.IsGestureBlockedAsync("code.exe", CancellationToken.None));
        Assert.True(service.IsGestureBlockedCached("code.exe"));

        await service.DeleteAsync(item.Id, CancellationToken.None);

        Assert.False(await service.IsGestureBlockedAsync("code.exe", CancellationToken.None));
        Assert.False(service.IsGestureBlockedCached("code.exe"));
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
            var path = Path.Combine(Path.GetTempPath(), $"gestureclip-blacklist-tests-{Guid.NewGuid():N}.db");
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
