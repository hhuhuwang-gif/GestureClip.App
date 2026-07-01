using Xunit;
using Dapper;
using GestureClip.Core.Gestures;
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
        var maxItems = await connection.ExecuteScalarAsync<string>("SELECT Value FROM Settings WHERE Key = 'Clipboard.MaxItems';");
        var retentionDays = await connection.ExecuteScalarAsync<string>("SELECT Value FROM Settings WHERE Key = 'Clipboard.RetentionDays';");
        var edgeEnabled = await connection.ExecuteScalarAsync<string>("SELECT Value FROM Settings WHERE Key = 'EdgeTrigger.Enabled';");
        var topLeftAction = await connection.ExecuteScalarAsync<string>("SELECT Value FROM Settings WHERE Key = 'EdgeTrigger.TopLeft.Action';");

        Assert.Equal(23, settingCount);
        Assert.Equal(7, blacklistCount);
        Assert.Equal(6, gestureCount);
        Assert.Equal("1000", maxItems);
        Assert.Equal("30", retentionDays);
        Assert.Equal("false", edgeEnabled);
        Assert.Equal(((int)BuiltInGestureAction.StartMenu).ToString(), topLeftAction);
    }

    [Fact]
    public async Task SeedAsync_does_not_overwrite_existing_clipboard_cleanup_settings()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var runner = new SqlMigrationRunner(NullLogger<SqlMigrationRunner>.Instance);
        await runner.RunAsync(connection, new[]
        {
            new SqlMigration(1, "initial", InitialMigration.Sql)
        }, CancellationToken.None);

        await connection.ExecuteAsync(
            """
INSERT INTO Settings (Key, Value, ValueType, UpdatedAt)
VALUES ('Clipboard.MaxItems', '500', 'int', @UpdatedAt),
       ('Clipboard.RetentionDays', '0', 'int', @UpdatedAt);
""",
            new { UpdatedAt = DateTimeOffset.UtcNow.ToString("O") });

        var seeder = new DefaultDataSeeder(NullLogger<DefaultDataSeeder>.Instance);

        await seeder.SeedAsync(connection, CancellationToken.None);

        var maxItems = await connection.ExecuteScalarAsync<string>("SELECT Value FROM Settings WHERE Key = 'Clipboard.MaxItems';");
        var retentionDays = await connection.ExecuteScalarAsync<string>("SELECT Value FROM Settings WHERE Key = 'Clipboard.RetentionDays';");

        Assert.Equal("500", maxItems);
        Assert.Equal("0", retentionDays);
    }
}

