using Dapper;
using GestureClip.Core.Gestures;
using GestureClip.Core.Privacy;
using GestureClip.Core.Settings;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;

namespace GestureClip.Features.Startup;

public sealed class DefaultDataSeeder
{
    private readonly ILogger<DefaultDataSeeder> _logger;

    public DefaultDataSeeder(ILogger<DefaultDataSeeder> logger)
    {
        _logger = logger;
    }

    public async Task SeedAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow.ToString("O");

        await SeedSettingAsync(connection, SettingKeys.AppStartWithWindows, "false", "bool", now);
        await SeedSettingAsync(connection, SettingKeys.ClipboardCaptureEnabled, "true", "bool", now);
        await SeedSettingAsync(connection, SettingKeys.ClipboardMaxItems, "1000", "int", now);
        await SeedSettingAsync(connection, SettingKeys.ClipboardRetentionDays, "30", "int", now);
        await SeedSettingAsync(connection, SettingKeys.HotkeyOpenClipboardOverlayEnabled, "true", "bool", now);
        await SeedSettingAsync(connection, SettingKeys.HotkeyOpenClipboardOverlayKey, "\"Ctrl+Alt+V\"", "string", now);
        await SeedSettingAsync(connection, SettingKeys.GestureEnabled, "true", "bool", now);
        await SeedSettingAsync(connection, SettingKeys.GestureShowOverlay, "true", "bool", now);
        await SeedSettingAsync(connection, SettingKeys.GestureDebugEnabled, "false", "bool", now);
        await SeedSettingAsync(connection, SettingKeys.GesturePreset, "0", "int", now);
        await SeedSettingAsync(connection, SettingKeys.GestureTriggerThreshold, "20", "int", now);
        await SeedSettingAsync(connection, SettingKeys.GestureSegmentThreshold, "16", "int", now);
        await SeedSettingAsync(connection, SettingKeys.GestureMaxDurationMs, "2000", "int", now);
        await SeedSettingAsync(connection, SettingKeys.GestureCloseWindowEnabled, "false", "bool", now);
        await SeedSettingAsync(connection, SettingKeys.EdgeTriggerEnabled, "false", "bool", now);
        await SeedSettingAsync(connection, SettingKeys.EdgeTriggerHotZoneSize, "8", "int", now);
        await SeedSettingAsync(connection, SettingKeys.EdgeTriggerDwellMs, "350", "int", now);
        await SeedSettingAsync(connection, SettingKeys.EdgeTriggerCooldownMs, "1200", "int", now);
        await SeedSettingAsync(connection, SettingKeys.EdgeTriggerSlideThreshold, "80", "int", now);
        await SeedSettingAsync(connection, SettingKeys.EdgeTriggerTopLeftAction, ((int)BuiltInGestureAction.StartMenu).ToString(), "int", now);
        await SeedSettingAsync(connection, SettingKeys.EdgeTriggerTopRightAction, ((int)BuiltInGestureAction.TaskSwitcher).ToString(), "int", now);
        await SeedSettingAsync(connection, SettingKeys.EdgeTriggerBottomRightAction, ((int)BuiltInGestureAction.ShowDesktop).ToString(), "int", now);
        await SeedSettingAsync(connection, SettingKeys.EdgeTriggerBottomLeftAction, ((int)BuiltInGestureAction.SwitchApp).ToString(), "int", now);
        await SeedSettingAsync(connection, SettingKeys.EdgeTriggerLeftEdgeLeftButtonEnabled, "false", "bool", now);
        await SeedSettingAsync(connection, SettingKeys.EdgeTriggerLeftEdgeLeftButtonAction, ((int)BuiltInGestureAction.StartMenu).ToString(), "int", now);
        await SeedSettingAsync(connection, SettingKeys.EdgeTriggerLeftEdgeMiddleButtonEnabled, "false", "bool", now);
        await SeedSettingAsync(connection, SettingKeys.EdgeTriggerLeftEdgeMiddleButtonAction, ((int)BuiltInGestureAction.ShowDesktop).ToString(), "int", now);
        await SeedSettingAsync(connection, SettingKeys.EdgeTriggerLeftEdgeXButton1Enabled, "false", "bool", now);
        await SeedSettingAsync(connection, SettingKeys.EdgeTriggerLeftEdgeXButton1Action, ((int)BuiltInGestureAction.SwitchApp).ToString(), "int", now);
        await SeedSettingAsync(connection, SettingKeys.EdgeTriggerLeftEdgeXButton2Enabled, "false", "bool", now);
        await SeedSettingAsync(connection, SettingKeys.EdgeTriggerLeftEdgeXButton2Action, ((int)BuiltInGestureAction.TaskSwitcher).ToString(), "int", now);
        await SeedSettingAsync(connection, SettingKeys.EdgeTriggerTopRightWheelEnabled, "false", "bool", now);
        await SeedSettingAsync(connection, SettingKeys.EdgeTriggerTopRightWheelAction, ((int)BuiltInGestureAction.TaskSwitcher).ToString(), "int", now);
        await SeedSettingAsync(connection, SettingKeys.EdgeTriggerSlideLeftEnabled, "false", "bool", now);
        await SeedSettingAsync(connection, SettingKeys.EdgeTriggerSlideLeftAction, ((int)BuiltInGestureAction.SwitchApp).ToString(), "int", now);
        await SeedSettingAsync(connection, SettingKeys.EdgeTriggerSlideRightEnabled, "false", "bool", now);
        await SeedSettingAsync(connection, SettingKeys.EdgeTriggerSlideRightAction, ((int)BuiltInGestureAction.TaskSwitcher).ToString(), "int", now);
        await SeedSettingAsync(connection, SettingKeys.EdgeTriggerSlideTopEnabled, "false", "bool", now);
        await SeedSettingAsync(connection, SettingKeys.EdgeTriggerSlideTopAction, ((int)BuiltInGestureAction.StartMenu).ToString(), "int", now);
        await SeedSettingAsync(connection, SettingKeys.EdgeTriggerSlideBottomEnabled, "false", "bool", now);
        await SeedSettingAsync(connection, SettingKeys.EdgeTriggerSlideBottomAction, ((int)BuiltInGestureAction.ShowDesktop).ToString(), "int", now);
        await SeedSettingAsync(connection, SettingKeys.PrivacySuppressSensitive, "true", "bool", now);

        foreach (var processName in DefaultPrivacyBlacklist.ProcessNames)
        {
            await connection.ExecuteAsync(
                """
INSERT OR IGNORE INTO AppBlacklist
    (Id, ProcessName, Reason, BlockClipboard, BlockGesture, CreatedAt, UpdatedAt)
VALUES
    (@Id, @ProcessName, @Reason, 1, 1, @CreatedAt, @UpdatedAt);
""",
                new
                {
                    Id = StableId("blacklist", processName),
                    ProcessName = processName,
                    Reason = "Default privacy blacklist",
                    CreatedAt = now,
                    UpdatedAt = now
                });
        }

        await SeedDefaultGestureAsync(connection, "U", "Open clipboard panel", "OpenClipboardPanel", true, now);
        await SeedDefaultGestureAsync(connection, "D", "Paste latest clipboard item", "PasteLatestClipboard", true, now);
        await SeedDefaultGestureAsync(connection, "L", "Navigate back", "SendAltLeft", true, now);
        await SeedDefaultGestureAsync(connection, "R", "Navigate forward", "SendAltRight", true, now);
        await SeedDefaultGestureAsync(connection, "DU", "Minimize current window", "MinimizeCurrentWindow", true, now);
        await SeedDefaultGestureAsync(connection, "UD", "Close current window", "CloseCurrentWindow", false, now);

        _logger.LogInformation("Default data seed completed.");
    }

    private static Task SeedSettingAsync(
        SqliteConnection connection,
        string key,
        string value,
        string valueType,
        string now)
    {
        return connection.ExecuteAsync(
            """
INSERT OR IGNORE INTO Settings (Key, Value, ValueType, UpdatedAt)
VALUES (@Key, @Value, @ValueType, @UpdatedAt);
""",
            new
            {
                Key = key,
                Value = value,
                ValueType = valueType,
                UpdatedAt = now
            });
    }

    private static async Task SeedDefaultGestureAsync(
        SqliteConnection connection,
        string pattern,
        string name,
        string actionType,
        bool isEnabled,
        string now)
    {
        var actionId = StableId("action", pattern);

        await connection.ExecuteAsync(
            """
INSERT OR IGNORE INTO Actions
    (Id, Name, ActionType, IsBuiltIn, IsEnabled, CreatedAt, UpdatedAt)
VALUES
    (@Id, @Name, @ActionType, 1, 1, @CreatedAt, @UpdatedAt);
""",
            new
            {
                Id = actionId,
                Name = name,
                ActionType = actionType,
                CreatedAt = now,
                UpdatedAt = now
            });

        await connection.ExecuteAsync(
            """
INSERT OR IGNORE INTO GestureRules
    (Id, Name, Pattern, ActionId, IsEnabled, IsDefault, CreatedAt, UpdatedAt)
VALUES
    (@Id, @Name, @Pattern, @ActionId, @IsEnabled, 1, @CreatedAt, @UpdatedAt);
""",
            new
            {
                Id = StableId("gesture", pattern),
                Name = name,
                Pattern = pattern,
                ActionId = actionId,
                IsEnabled = isEnabled ? 1 : 0,
                CreatedAt = now,
                UpdatedAt = now
            });
    }

    private static string StableId(string scope, string value)
    {
        var bytes = SHA1.HashData(Encoding.UTF8.GetBytes($"{scope}:{value}"));
        var guidBytes = bytes.Take(16).ToArray();
        return new Guid(guidBytes).ToString();
    }
}
