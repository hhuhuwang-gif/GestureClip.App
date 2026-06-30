using Xunit;
using Dapper;
using GestureClip.Features.Startup;
using GestureClip.Infrastructure.Database;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;

namespace GestureClip.Tests.Database;

public sealed class DefaultDataSeederTests
{
    [Fact]
    public async Task SeedAsync_can_run_repeatedly_without_duplicate_defaults()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var runner = new SqlMigrationRunner(NullLogger<SqlMigrationRunner>.Instance);
        await runner.RunAsync(connection, new[]
        {
            new SqlMigration(1, "initial", InitialMigration.Sql)
        }, CancellationToken.None);

        var seeder = new DefaultDataSeeder(NullLogger<DefaultDataSeeder>.Instance);

        await seeder.SeedAsync(connection, CancellationToken.None);
        await seeder.SeedAsync(connection, CancellationToken.None);

        var settingCount = await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM Settings;");
        var blacklistCount = await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM AppBlacklist;");
        var gestureCount = await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM GestureRules;");

        Assert.Equal(13, settingCount);
        Assert.Equal(7, blacklistCount);
        Assert.Equal(6, gestureCount);
    }
}

