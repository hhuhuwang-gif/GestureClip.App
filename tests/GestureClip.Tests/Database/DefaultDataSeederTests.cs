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
        var perfLogEnabled = await connection.ExecuteScalarAsync<string>("SELECT Value FROM Settings WHERE Key = 'Clipboard.PerfLogEnabled';");
        var hotkey = await connection.ExecuteScalarAsync<string>("SELECT Value FROM Settings WHERE Key = 'Hotkey.OpenClipboardOverlay.Key';");
        var edgeEnabled = await connection.ExecuteScalarAsync<string>("SELECT Value FROM Settings WHERE Key = 'EdgeTrigger.Enabled';");
        var rightEnabled = await connection.ExecuteScalarAsync<string>("SELECT Value FROM Settings WHERE Key = 'Gesture.Trigger.RightButton.Enabled';");
        var leftEnabled = await connection.ExecuteScalarAsync<string>("SELECT Value FROM Settings WHERE Key = 'Gesture.Trigger.LeftButton.Enabled';");
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
        var style = await connection.ExecuteScalarAsync<string>("SELECT Value FROM Settings WHERE Key = 'Workstation.CopywritingStyle';");
        var workerXp = await connection.ExecuteScalarAsync<string>("SELECT Value FROM Settings WHERE Key = 'WorkerLevel.TotalXp';");
        var workerLevel = await connection.ExecuteScalarAsync<string>("SELECT Value FROM Settings WHERE Key = 'WorkerLevel.CurrentLevel';");
        var showPopup = await connection.ExecuteScalarAsync<string>("SELECT Value FROM Settings WHERE Key = 'WorkerLevel.ShowLevelUpPopup';");
        var showLevelInHud = await connection.ExecuteScalarAsync<string>("SELECT Value FROM Settings WHERE Key = 'WorkerLevel.ShowLevelInHud';");
        var funText = await connection.ExecuteScalarAsync<string>("SELECT Value FROM Settings WHERE Key = 'Hud.FunTextEnabled';");
        var statusLevel = await connection.ExecuteScalarAsync<string>("SELECT Value FROM Settings WHERE Key = 'Hud.StatusLevelEnabled';");
        var copywritingStyle = await connection.ExecuteScalarAsync<string>("SELECT Value FROM Settings WHERE Key = 'Workstation.CopywritingStyle';");
        var overworkEnabled = await connection.ExecuteScalarAsync<string>("SELECT Value FROM Settings WHERE Key = 'Workstation.OverworkReminder.Enabled';");
        var overworkInterval = await connection.ExecuteScalarAsync<string>("SELECT Value FROM Settings WHERE Key = 'Workstation.OverworkReminder.IntervalMinutes';");
        var overworkRisk = await connection.ExecuteScalarAsync<string>("SELECT Value FROM Settings WHERE Key = 'Workstation.OverworkReminder.HighRiskAfterHours';");
        var hudTimeColor = await connection.ExecuteScalarAsync<string>("SELECT Value FROM Settings WHERE Key = 'Workstation.HudTimeColor.Enabled';");
        var strongWarning = await connection.ExecuteScalarAsync<string>("SELECT Value FROM Settings WHERE Key = 'Workstation.OverworkReminder.StrongWarning.Enabled';");
        var canSnooze = await connection.ExecuteScalarAsync<string>("SELECT Value FROM Settings WHERE Key = 'Workstation.OverworkReminder.CanSnooze';");
        var snoozeMinutes = await connection.ExecuteScalarAsync<string>("SELECT Value FROM Settings WHERE Key = 'Workstation.OverworkReminder.SnoozeMinutes';");

        Assert.Equal(76, settingCount);
        Assert.Equal(7, blacklistCount);
        Assert.Equal(12, gestureCount);
        Assert.Equal("1000", maxItems);
        Assert.Equal("30", retentionDays);
        Assert.Equal("5242880", maxImageBytes);
        Assert.Equal("false", perfLogEnabled);
        Assert.Equal("\"Ctrl + `\"", hotkey);
        Assert.Equal("true", rightEnabled);
        Assert.Equal("false", leftEnabled);
        Assert.Equal("false", middleEnabled);
        Assert.Equal("false", x1Enabled);
        Assert.Equal("false", x2Enabled);
        Assert.Equal("false", edgeEnabled);
        Assert.Equal(((int)BuiltInGestureAction.StartMenu).ToString(), topLeftAction);
        Assert.Equal("false", leftEdgeLeftEnabled);
        Assert.Equal("false", leftEdgeMiddleEnabled);
        Assert.Equal("false", topRightWheelEnabled);
        Assert.Equal("160", dwellMs);
        Assert.Equal("450", cooldownMs);
        Assert.Equal("56", slideThreshold);
        Assert.Equal("false", slideLeftEnabled);
        Assert.Equal(((int)BuiltInGestureAction.PasteAndEnter).ToString(), slideBottomAction);
        Assert.Equal("0", monthlySalary);
        Assert.Equal("\"09:00\"", workStart);
        Assert.Equal("\"18:00\"", workEnd);
        Assert.Equal("15", payday);
        Assert.Equal("0", workerXp);
        Assert.Equal("1", workerLevel);
        Assert.Equal("true", showPopup);
        Assert.Equal("true", showLevelInHud);
        Assert.Equal("true", funText);
        Assert.Equal("true", statusLevel);
        Assert.Equal("\"打工人模式\"", copywritingStyle);
        Assert.Equal("true", overworkEnabled);
        Assert.Equal("60", overworkInterval);
        Assert.Equal("8", overworkRisk);
        Assert.Equal("true", hudTimeColor);
        Assert.Equal("false", strongWarning);
        Assert.Equal("true", canSnooze);
        Assert.Equal("15", snoozeMinutes);
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
       ('Workstation.Payday', '10', 'int', @UpdatedAt),
       ('Workstation.CopywritingStyle', '"抽象模式"', 'string', @UpdatedAt);
""",
            new { UpdatedAt = DateTimeOffset.UtcNow.ToString("O") });

        var seeder = new DefaultDataSeeder(NullLogger<DefaultDataSeeder>.Instance);

        await seeder.SeedAsync(connection, CancellationToken.None);

        var monthlySalary = await connection.ExecuteScalarAsync<string>("SELECT Value FROM Settings WHERE Key = 'Workstation.MonthlySalary';");
        var workStart = await connection.ExecuteScalarAsync<string>("SELECT Value FROM Settings WHERE Key = 'Workstation.WorkStartTime';");
        var payday = await connection.ExecuteScalarAsync<string>("SELECT Value FROM Settings WHERE Key = 'Workstation.Payday';");
        var style = await connection.ExecuteScalarAsync<string>("SELECT Value FROM Settings WHERE Key = 'Workstation.CopywritingStyle';");

        Assert.Equal("18000", monthlySalary);
        Assert.Equal("\"10:00\"", workStart);
        Assert.Equal("10", payday);
        Assert.Equal("\"抽象模式\"", style);
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


    [Fact]
    public async Task SeedAsync_does_not_overwrite_existing_worker_level_settings()
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
VALUES ('WorkerLevel.TotalXp', '88', 'int', @UpdatedAt),
       ('WorkerLevel.CurrentLevel', '3', 'int', @UpdatedAt),
       ('Hud.FunTextEnabled', 'false', 'bool', @UpdatedAt);
""",
            new { UpdatedAt = DateTimeOffset.UtcNow.ToString("O") });

        var seeder = new DefaultDataSeeder(NullLogger<DefaultDataSeeder>.Instance);

        await seeder.SeedAsync(connection, CancellationToken.None);

        var workerXp = await connection.ExecuteScalarAsync<string>("SELECT Value FROM Settings WHERE Key = 'WorkerLevel.TotalXp';");
        var workerLevel = await connection.ExecuteScalarAsync<string>("SELECT Value FROM Settings WHERE Key = 'WorkerLevel.CurrentLevel';");
        var funText = await connection.ExecuteScalarAsync<string>("SELECT Value FROM Settings WHERE Key = 'Hud.FunTextEnabled';");

        Assert.Equal("88", workerXp);
        Assert.Equal("3", workerLevel);
        Assert.Equal("false", funText);
    }

    [Fact]
    public async Task SeedAsync_does_not_force_optional_gesture_triggers_on()
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
VALUES ('Gesture.Trigger.MiddleButton.Enabled', 'false', 'bool', @UpdatedAt),
       ('Gesture.Trigger.XButton1.Enabled', 'false', 'bool', @UpdatedAt),
       ('Gesture.Trigger.XButton2.Enabled', 'false', 'bool', @UpdatedAt),
       ('EdgeTrigger.Enabled', 'false', 'bool', @UpdatedAt);
""",
            new { UpdatedAt = DateTimeOffset.UtcNow.ToString("O") });

        var seeder = new DefaultDataSeeder(NullLogger<DefaultDataSeeder>.Instance);

        await seeder.SeedAsync(connection, CancellationToken.None);

        var values = await connection.QueryAsync<(string Key, string Value)>(
            """
SELECT Key, Value FROM Settings
WHERE Key IN ('Gesture.Trigger.MiddleButton.Enabled',
              'Gesture.Trigger.XButton1.Enabled',
              'Gesture.Trigger.XButton2.Enabled',
              'EdgeTrigger.Enabled')
ORDER BY Key;
""");

        Assert.All(values, row => Assert.Equal("false", row.Value));
    }

}
