using GestureClip.Core.Workstation;
using GestureClip.Features.Workstation;
using GestureClip.Infrastructure.Database;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GestureClip.Tests.Workstation;

public sealed class WorkstationStatsRepositoryTests
{
    [Fact]
    public async Task GetOrCreateAsync_creates_empty_row_for_date()
    {
        using var database = await TestDatabase.CreateAsync();
        var repository = new WorkstationStatsRepository(database.ConnectionFactory);
        var date = new DateOnly(2026, 7, 6);

        var stats = await repository.GetOrCreateAsync(date, CancellationToken.None);

        Assert.Equal(date, stats.Date);
        Assert.Equal(0, stats.CopyCount);
        Assert.Equal(0, stats.PasteCount);
        Assert.Equal(0, stats.GestureCount);
    }

    [Fact]
    public async Task SaveAsync_persists_daily_counts_and_fishing_state()
    {
        using var database = await TestDatabase.CreateAsync();
        var repository = new WorkstationStatsRepository(database.ConnectionFactory);
        var date = new DateOnly(2026, 7, 6);
        var startedAt = new DateTimeOffset(2026, 7, 6, 15, 0, 0, TimeSpan.FromHours(8));

        await repository.SaveAsync(new WorkstationDailyStats(date, 3, 4, 5, 15, 20, startedAt), CancellationToken.None);
        var stored = await repository.GetOrCreateAsync(date, CancellationToken.None);

        Assert.Equal(3, stored.CopyCount);
        Assert.Equal(4, stored.PasteCount);
        Assert.Equal(5, stored.GestureCount);
        Assert.Equal(15, stored.EstimatedSavedClicks);
        Assert.Equal(20, stored.FishingMinutes);
        Assert.Equal(startedAt, stored.FishingStartedAt);
    }

    [Fact]
    public async Task IncrementCountersAsync_updates_counts_with_single_atomic_operation()
    {
        using var database = await TestDatabase.CreateAsync();
        var repository = new WorkstationStatsRepository(database.ConnectionFactory);
        var date = new DateOnly(2026, 7, 6);

        await repository.IncrementCountersAsync(date, copyDelta: 1, pasteDelta: 0, gestureDelta: 0, savedClicksDelta: 0, CancellationToken.None);
        await repository.IncrementCountersAsync(date, copyDelta: 0, pasteDelta: 1, gestureDelta: 0, savedClicksDelta: 0, CancellationToken.None);
        await repository.IncrementCountersAsync(date, copyDelta: 0, pasteDelta: 0, gestureDelta: 1, savedClicksDelta: 3, CancellationToken.None);
        var stored = await repository.GetOrCreateAsync(date, CancellationToken.None);

        Assert.Equal(1, stored.CopyCount);
        Assert.Equal(1, stored.PasteCount);
        Assert.Equal(1, stored.GestureCount);
        Assert.Equal(3, stored.EstimatedSavedClicks);
    }

    [Fact]
    public async Task ResetAsync_clears_existing_row()
    {
        using var database = await TestDatabase.CreateAsync();
        var repository = new WorkstationStatsRepository(database.ConnectionFactory);
        var date = new DateOnly(2026, 7, 6);
        await repository.SaveAsync(new WorkstationDailyStats(date, 3, 4, 5, 15, 20, DateTimeOffset.UtcNow), CancellationToken.None);

        await repository.ResetAsync(date, CancellationToken.None);
        var stored = await repository.GetOrCreateAsync(date, CancellationToken.None);

        Assert.Equal(new WorkstationDailyStats(date), stored);
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
            await runner.RunAsync(
                connection,
                [
                    new SqlMigration(1, "initial", InitialMigration.Sql),
                    new SqlMigration(2, "workstation_stats", WorkstationStatsMigration.Sql)
                ],
                CancellationToken.None);
            await WorkstationHubMigration.EnsureAsync(connection);
            await runner.RunAsync(
                connection,
                [
                    new SqlMigration(6, "workstation_hub_stats", WorkstationHubMigration.Sql)
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
