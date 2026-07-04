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
        var maxImageBytes = await connection.ExecuteScalarAsync<string>("SELECT Value FROM Settings WHERE Key = 'Clipboard.MaxImageBytes';");
        var hotkey = await connection.ExecuteScalarAsync<string>("SELECT Value FROM Settings WHERE Key = 'Hotkey.OpenClipboardOverlay.Key';");
        var edgeEnabled = await connection.ExecuteScalarAsync<string>("SELECT Value FROM Settings WHERE Key = 'EdgeTrigger.Enabled';");
        var middleEnabled = await connection.ExecuteScalarAsync<string>("SELECT Value FROM Settings WHERE Key = 'Gesture.Trigger.MiddleButton.Enabled';");
        var x1Enabled = await connection.ExecuteScalarAsync<string>("SELECT Value FROM Settings WHERE Key = 'Gesture.Trigger.XButton1.Enabled';");
        var x2Enabled = await connection.ExecuteScalarAsync<string>("SELECT Value FROM Settings WHERE Key = 'Gesture.Trigger.XButton2.Enabled';");
        var topLeftAction = await connection.ExecuteScalarAsync<string>("SELECT Value FROM Settings WHERE Key = 'EdgeTrigger.TopLeft.Action';");
        var leftEdgeMiddleEnabled = await connection.ExecuteScalarAsync<string>("SELECT Value FROM Settings WHERE Key = 'EdgeTrigger.LeftEdge.MiddleButton.Enabled';");
        var leftEdgeLeftEnabled = await connection.ExecuteScalarAsync<string>("SELECT Value FROM Settings WHERE Key = 'EdgeTrigger.LeftEdge.LeftButton.Enabled';");
        var topRightWheelEnabled = await connection.ExecuteScalarAsync<string>("SELECT Value FROM Settings WHERE Key = 'EdgeTrigger.TopRight.Wheel.Enabled';");
        var slideThreshold = await connection.ExecuteScalarAsync<string>("SELECT Value FROM Settings WHERE Key = 'EdgeTrigger.SlideThreshold';");
        var dwellMs = await connection.ExecuteScalarAsync<string>("SELECT Value FROM Settings WHERE Key = 'EdgeTrigger.DwellMs';");
        var cooldownMs = await connection.ExecuteScalarAsync<string>("SELECT Value FROM Settings WHERE Key = 'EdgeTrigger.CooldownMs';");
        var slideLeftEnabled = await connection.ExecuteScalarAsync<string>("SELECT Value FROM Settings WHERE Key = 'EdgeTrigger.Slide.Left.Enabled';");
        var slideBottomAction = await connection.ExecuteScalarAsync<string>("SELECT Value FROM Settings WHERE Key = 'EdgeTrigger.Slide.Bottom.Action';");
        var monthlySalary = await connection.ExecuteScalarAsync<string>("SELECT Value FROM Settings WHERE Key = 'Workstation.MonthlySalary';");
        var workStart = await connection.ExecuteScalarAsync<string>("SELECT Value FROM Settings WHERE Key = 'Workstation.WorkStartTime';");
        var workEnd = await connection.ExecuteScalarAsync<string>("SELECT Value FROM Settings WHERE Key = 'Workstation.WorkEndTime';");
        var payday = await connection.ExecuteScalarAsync<string>("SELECT Value FROM Settings WHERE Key = 'Workstation.Payday';");

        Assert.Equal(57, settingCount);
        Assert.Equal(7, blacklistCount);
        Assert.Equal(6, gestureCount);
        Assert.Equal("1000", maxItems);
        Assert.Equal("30", retentionDays);
        Assert.Equal("5242880", maxImageBytes);
        Assert.Equal("\"Ctrl + `\"", hotkey);
        Assert.Equal("true", middleEnabled);
        Assert.Equal("true", x1Enabled);
        Assert.Equal("true", x2Enabled);
        Assert.Equal("true", edgeEnabled);
        Assert.Equal(((int)BuiltInGestureAction.StartMenu).ToString(), topLeftAction);
        Assert.Equal("false", leftEdgeLeftEnabled);
        Assert.Equal("true", leftEdgeMiddleEnabled);
        Assert.Equal("true", topRightWheelEnabled);
        Assert.Equal("160", dwellMs);
        Assert.Equal("450", cooldownMs);
        Assert.Equal("56", slideThreshold);
        Assert.Equal("true", slideLeftEnabled);
        Assert.Equal(((int)BuiltInGestureAction.PasteAndEnter).ToString(), slideBottomAction);
        Assert.Equal("0", monthlySalary);
        Assert.Equal("\"09:00\"", workStart);
        Assert.Equal("\"18:00\"", workEnd);
        Assert.Equal("15", payday);
    }

    [Fact]
    public async Task SeedAsync_turns_legacy_left_edge_left_button_off()
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
VALUES ('EdgeTrigger.LeftEdge.LeftButton.Enabled', 'true', 'bool', @UpdatedAt);
""",
            new { UpdatedAt = DateTimeOffset.UtcNow.ToString("O") });

        var seeder = new DefaultDataSeeder(NullLogger<DefaultDataSeeder>.Instance);

        await seeder.SeedAsync(connection, CancellationToken.None);

        var enabled = await connection.ExecuteScalarAsync<string>("SELECT Value FROM Settings WHERE Key = 'EdgeTrigger.LeftEdge.LeftButton.Enabled';");
        Assert.Equal("false", enabled);
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
       ('Clipboard.RetentionDays', '0', 'int', @UpdatedAt),
       ('Clipboard.MaxImageBytes', '1048576', 'int', @UpdatedAt);
""",
            new { UpdatedAt = DateTimeOffset.UtcNow.ToString("O") });

        var seeder = new DefaultDataSeeder(NullLogger<DefaultDataSeeder>.Instance);

        await seeder.SeedAsync(connection, CancellationToken.None);

        var maxItems = await connection.ExecuteScalarAsync<string>("SELECT Value FROM Settings WHERE Key = 'Clipboard.MaxItems';");
        var retentionDays = await connection.ExecuteScalarAsync<string>("SELECT Value FROM Settings WHERE Key = 'Clipboard.RetentionDays';");
        var maxImageBytes = await connection.ExecuteScalarAsync<string>("SELECT Value FROM Settings WHERE Key = 'Clipboard.MaxImageBytes';");

        Assert.Equal("500", maxItems);
        Assert.Equal("0", retentionDays);
        Assert.Equal("1048576", maxImageBytes);
    }

    [Fact]
    public async Task SeedAsync_does_not_overwrite_existing_hotkey_setting()
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
VALUES ('Hotkey.OpenClipboardOverlay.Key', '"Ctrl+Shift+V"', 'string', @UpdatedAt);
""",
            new { UpdatedAt = DateTimeOffset.UtcNow.ToString("O") });

        var seeder = new DefaultDataSeeder(NullLogger<DefaultDataSeeder>.Instance);

        await seeder.SeedAsync(connection, CancellationToken.None);

        var hotkey = await connection.ExecuteScalarAsync<string>("SELECT Value FROM Settings WHERE Key = 'Hotkey.OpenClipboardOverlay.Key';");

        Assert.Equal("\"Ctrl+Shift+V\"", hotkey);
    }

    [Fact]
    public async Task SeedAsync_does_not_overwrite_existing_workstation_settings()
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
VALUES ('Workstation.MonthlySalary', '18000', 'decimal', @UpdatedAt),
       ('Workstation.WorkStartTime', '"10:00"', 'string', @UpdatedAt),
       ('Workstation.Payday', '10', 'int', @UpdatedAt);
""",
            new { UpdatedAt = DateTimeOffset.UtcNow.ToString("O") });

        var seeder = new DefaultDataSeeder(NullLogger<DefaultDataSeeder>.Instance);

        await seeder.SeedAsync(connection, CancellationToken.None);

        var monthlySalary = await connection.ExecuteScalarAsync<string>("SELECT Value FROM Settings WHERE Key = 'Workstation.MonthlySalary';");
        var workStart = await connection.ExecuteScalarAsync<string>("SELECT Value FROM Settings WHERE Key = 'Workstation.WorkStartTime';");
        var payday = await connection.ExecuteScalarAsync<string>("SELECT Value FROM Settings WHERE Key = 'Workstation.Payday';");

        Assert.Equal("18000", monthlySalary);
        Assert.Equal("\"10:00\"", workStart);
        Assert.Equal("10", payday);
    }

    [Fact]
    public async Task SeedAsync_migrates_old_default_hotkey_to_new_default()
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
VALUES ('Hotkey.OpenClipboardOverlay.Key', '"Ctrl+Alt+V"', 'string', @UpdatedAt);
""",
            new { UpdatedAt = DateTimeOffset.UtcNow.ToString("O") });

        var seeder = new DefaultDataSeeder(NullLogger<DefaultDataSeeder>.Instance);

        await seeder.SeedAsync(connection, CancellationToken.None);

        var hotkey = await connection.ExecuteScalarAsync<string>("SELECT Value FROM Settings WHERE Key = 'Hotkey.OpenClipboardOverlay.Key';");

        Assert.Equal("\"Ctrl + `\"", hotkey);
    }

    [Fact]
    public async Task SeedAsync_migrates_old_edge_timing_defaults_to_responsive_values()
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
VALUES ('EdgeTrigger.DwellMs', '350', 'int', @UpdatedAt),
       ('EdgeTrigger.CooldownMs', '1200', 'int', @UpdatedAt),
       ('EdgeTrigger.SlideThreshold', '80', 'int', @UpdatedAt);
""",
            new { UpdatedAt = DateTimeOffset.UtcNow.ToString("O") });

        var seeder = new DefaultDataSeeder(NullLogger<DefaultDataSeeder>.Instance);

        await seeder.SeedAsync(connection, CancellationToken.None);

        var dwellMs = await connection.ExecuteScalarAsync<string>("SELECT Value FROM Settings WHERE Key = 'EdgeTrigger.DwellMs';");
        var cooldownMs = await connection.ExecuteScalarAsync<string>("SELECT Value FROM Settings WHERE Key = 'EdgeTrigger.CooldownMs';");
        var slideThreshold = await connection.ExecuteScalarAsync<string>("SELECT Value FROM Settings WHERE Key = 'EdgeTrigger.SlideThreshold';");

        Assert.Equal("160", dwellMs);
        Assert.Equal("450", cooldownMs);
        Assert.Equal("56", slideThreshold);
    }
}

