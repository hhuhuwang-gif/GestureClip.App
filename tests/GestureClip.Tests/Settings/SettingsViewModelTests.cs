using GestureClip.App.ViewModels;
using GestureClip.Core.Abstractions;
using GestureClip.Core.Clipboard;
using GestureClip.Core.Diagnostics;
using GestureClip.Core.Gestures;
using GestureClip.Core.Hotkeys;
using GestureClip.Core.Privacy;
using GestureClip.Core.Runtime;
using GestureClip.Core.Settings;
using GestureClip.Core.SystemInfo;
using GestureClip.Features.Gestures;
using GestureClip.Infrastructure.Paths;
using Xunit;

namespace GestureClip.Tests.Settings;

public sealed class SettingsViewModelTests
{
    [Fact]
    public async Task RefreshClipboardStatsAsync_updates_clipboard_item_count()
    {
        var repository = new FakeClipboardRepository { Count = 3 };
        var viewModel = CreateViewModel(repository: repository);

        await viewModel.RefreshClipboardStatsAsync();

        Assert.Equal(3, viewModel.ClipboardItemCount);
    }

    [Fact]
    public async Task ClearAllClipboardItemsAsync_deletes_when_user_confirms()
    {
        var repository = new FakeClipboardRepository { Count = 4 };
        var overlay = new FakeClipboardOverlayService();
        var confirmation = new FakeConfirmationService { Result = true };
        var viewModel = CreateViewModel(repository: repository, overlay: overlay, confirmation: confirmation);

        await viewModel.ClearAllClipboardItemsAsync();

        Assert.Equal(1, repository.ClearAllCount);
        Assert.Equal(1, overlay.RefreshCount);
        Assert.Equal(0, viewModel.ClipboardItemCount);
    }

    [Fact]
    public async Task ClearAllClipboardItemsAsync_does_not_delete_when_user_cancels()
    {
        var repository = new FakeClipboardRepository { Count = 4 };
        var confirmation = new FakeConfirmationService { Result = false };
        var viewModel = CreateViewModel(repository: repository, confirmation: confirmation);

        await viewModel.ClearAllClipboardItemsAsync();

        Assert.Equal(0, repository.ClearAllCount);
        Assert.Equal(4, viewModel.ClipboardItemCount);
    }

    [Fact]
    public async Task ClearUnpinnedClipboardItemsAsync_calls_repository_and_refreshes_overlay()
    {
        var repository = new FakeClipboardRepository { Count = 5 };
        var overlay = new FakeClipboardOverlayService();
        var viewModel = CreateViewModel(repository: repository, overlay: overlay, confirmation: new FakeConfirmationService { Result = true });

        await viewModel.ClearUnpinnedClipboardItemsAsync();

        Assert.Equal(1, repository.ClearUnpinnedCount);
        Assert.Equal(1, overlay.RefreshCount);
    }

    [Fact]
    public async Task ApplyClipboardCleanupAsync_calls_repository_with_current_settings()
    {
        var repository = new FakeClipboardRepository { Count = 8 };
        var viewModel = CreateViewModel(repository: repository, confirmation: new FakeConfirmationService { Result = true });
        viewModel.ClipboardMaxItems = 500;
        viewModel.SelectedClipboardRetentionOption = viewModel.ClipboardRetentionOptions.Single(option => option.Days == 90);

        await viewModel.ApplyClipboardCleanupAsync();

        Assert.Equal((500, 90), repository.LastCleanup);
    }

    [Fact]
    public async Task Clipboard_cleanup_setting_changes_are_saved()
    {
        var settings = new FakeSettingsService();
        var viewModel = CreateViewModel(settings: settings);

        viewModel.ClipboardMaxItems = 5000;
        viewModel.SelectedClipboardRetentionOption = viewModel.ClipboardRetentionOptions.Single(option => option.Days == 0);

        await WaitForAsync(() =>
            settings.Values.TryGetValue(SettingKeys.ClipboardMaxItems, out var maxItems) && Equals(maxItems, 5000) &&
            settings.Values.TryGetValue(SettingKeys.ClipboardRetentionDays, out var retentionDays) && Equals(retentionDays, 0));
    }

    [Fact]
    public async Task Changing_clipboard_hotkey_saves_setting_and_restarts_hotkey_service()
    {
        var settings = new FakeSettingsService();
        var hotkey = new FakeGlobalHotkeyService();
        var viewModel = CreateViewModel(settings: settings, hotkey: hotkey);

        viewModel.OpenClipboardHotkeyText = "Ctrl+Alt+V";

        await WaitForAsync(() =>
            settings.Values.TryGetValue(SettingKeys.HotkeyOpenClipboardOverlayKey, out var value) &&
            Equals(value, "Ctrl + Alt + V"));
        Assert.Equal(1, hotkey.StopCount);
        Assert.Equal(1, hotkey.StartCount);
    }

    [Fact]
    public void Gesture_action_quick_commands_set_new_and_selected_actions()
    {
        var viewModel = CreateViewModel();

        viewModel.SetNewGestureActionCommand.Execute("SearchSelectedTextWithBaidu");
        viewModel.SetSelectedGestureActionCommand.Execute("SearchSelectedTextWithGoogle");

        Assert.Equal(BuiltInGestureAction.SearchSelectedTextWithBaidu, viewModel.NewGestureAction);
        Assert.Equal(BuiltInGestureAction.SearchSelectedTextWithGoogle, viewModel.SelectedGestureBindingCard!.SelectedAction);
    }

    [Fact]
    public void Gesture_binding_cards_are_created_for_supported_patterns()
    {
        var viewModel = CreateViewModel();

        Assert.True(viewModel.GestureBindingCards.Count >= 20);
        Assert.Contains(viewModel.GestureBindingCards, card => card.Pattern == "U" && card.SelectedAction == BuiltInGestureAction.Copy);
        Assert.Contains(viewModel.GestureBindingCards, card => card.Pattern == "D" && card.SelectedAction == BuiltInGestureAction.Paste);
        Assert.Contains(viewModel.GestureBindingCards, card => card.Pattern == "DL" && card.SelectedAction == BuiltInGestureAction.PasteAndEnter);
        Assert.Contains(viewModel.PrimaryGestureBindingCards, card => card.Pattern == "DL");
        Assert.Contains(viewModel.PrimaryGestureBindingCards, card => card.Pattern == "DR" && card.SelectedAction == BuiltInGestureAction.NewTab);
        Assert.Contains(viewModel.PrimaryGestureBindingCards, card => card.Pattern == "UR" && card.SelectedAction == BuiltInGestureAction.SearchSelectedTextWithGoogle);
        Assert.Contains(viewModel.PrimaryGestureBindingCards, card => card.Pattern == "UL" && card.SelectedAction == BuiltInGestureAction.SearchSelectedTextWithBaidu);
        Assert.Contains(viewModel.AdvancedGestureBindingCards, card => card.Pattern == "URD" && card.SelectedAction == BuiltInGestureAction.None);
        Assert.Contains(viewModel.AdvancedGestureBindingCards, card => card.Pattern == "RDL" && card.SelectedAction == BuiltInGestureAction.None);
    }

    [Fact]
    public void Gesture_action_options_render_as_readable_text()
    {
        var viewModel = CreateViewModel();
        var option = viewModel.GestureActionOptions.Single(item => item.Action == BuiltInGestureAction.StartMenu);

        Assert.Equal(option.DisplayName, option.ToString());
        Assert.DoesNotContain(nameof(GestureActionOptionViewModel), option.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(viewModel.GestureActionOptions, item => item.Action == BuiltInGestureAction.LeftMouseClick);
        Assert.DoesNotContain(viewModel.GestureActionOptions, item => item.Action == BuiltInGestureAction.RightMouseClick);
    }

    [Fact]
    public void Gesture_trigger_modes_show_only_global_trigger_choices_without_left_button_trigger()
    {
        var viewModel = CreateViewModel();

        Assert.Contains(viewModel.GestureTriggerModes, mode => mode.Name == "鼠标右键" && mode.IsEnabled && mode.Status == "默认推荐");
        Assert.Contains(viewModel.GestureTriggerModes, mode => mode.Name == "鼠标中键" && mode.Status == "可选");
        Assert.Contains(viewModel.GestureTriggerModes, mode => mode.Name == "鼠标侧键 1" && mode.Status == "可选");
        Assert.Contains(viewModel.GestureTriggerModes, mode => mode.Name == "鼠标侧键 2" && mode.Status == "可选");
        Assert.Contains(viewModel.GestureTriggerModes, mode => mode.Name == "屏幕边缘 + 鼠标滑动" && mode.Status == "可选");
        Assert.Equal(5, viewModel.GestureTriggerModes.Count);
        Assert.DoesNotContain(viewModel.GestureTriggerModes, mode => mode.Name.Contains("左键", StringComparison.Ordinal));
        Assert.Equal(BuiltInGestureAction.PasteAndEnter, viewModel.EdgeTriggerSlideBottomAction);
    }


    [Fact]
    public async Task Gesture_trigger_summary_reflects_enabled_optional_triggers()
    {
        var settings = new FakeSettingsService();
        var viewModel = CreateViewModel(settings: settings);

        Assert.Equal("当前启用：鼠标右键", viewModel.EnabledGestureTriggerSummary);

        viewModel.GestureMiddleButtonEnabled = true;
        viewModel.GestureXButton1Enabled = true;
        viewModel.EdgeTriggerEnabled = true;

        await WaitForAsync(() =>
            settings.Values.TryGetValue(SettingKeys.GestureTriggerMiddleButtonEnabled, out var middle) && Equals(middle, true) &&
            settings.Values.TryGetValue(SettingKeys.GestureTriggerXButton1Enabled, out var x1) && Equals(x1, true) &&
            settings.Values.TryGetValue(SettingKeys.EdgeTriggerEnabled, out var edge) && Equals(edge, true));

        Assert.Equal("当前启用：鼠标右键 + 鼠标中键 + 鼠标侧键 1 + 屏幕边缘", viewModel.EnabledGestureTriggerSummary);
    }
    [Fact]
    public void Custom_gesture_direction_buttons_build_pattern_preview()
    {
        var viewModel = CreateViewModel();

        viewModel.AppendGestureDirectionCommand.Execute("D");
        viewModel.AppendGestureDirectionCommand.Execute("R");

        Assert.Equal("DR", viewModel.NewGesturePattern);
        Assert.Equal("↓→", viewModel.NewGestureDirectionPreview);

        viewModel.RemoveLastGestureDirectionCommand.Execute(null);

        Assert.Equal("D", viewModel.NewGesturePattern);
        Assert.Equal("↓", viewModel.NewGestureDirectionPreview);

        viewModel.ClearNewGesturePatternCommand.Execute(null);

        Assert.Equal("", viewModel.NewGesturePattern);
        Assert.Equal("点击方向按钮设计手势", viewModel.NewGestureDirectionPreview);
    }


    [Fact]
    public void Custom_gesture_template_sets_pattern_and_action_together()
    {
        var viewModel = CreateViewModel();

        viewModel.SetNewGestureTemplateCommand.Execute("RDU|SearchSelectedTextWithGoogle");

        Assert.Equal("RDU", viewModel.NewGesturePattern);
        Assert.Equal(BuiltInGestureAction.SearchSelectedTextWithGoogle, viewModel.NewGestureAction);
    }
    [Fact]
    public void Custom_gesture_template_buttons_set_complex_pattern()
    {
        var viewModel = CreateViewModel();

        viewModel.SetNewGesturePatternCommand.Execute("URD");

        Assert.Equal("URD", viewModel.NewGesturePattern);
        Assert.Equal("↑→↓", viewModel.NewGestureDirectionPreview);
        Assert.Equal("添加到列表", viewModel.NewGestureAddButtonText);
    }

    [Fact]
    public void Recording_custom_gesture_sets_recognized_pattern()
    {
        var viewModel = CreateViewModel();
        var now = DateTimeOffset.UtcNow;

        viewModel.SetNewGesturePatternFromRecordedPoints(
        [
            new GesturePoint(0, 0, now),
            new GesturePoint(60, 0, now.AddMilliseconds(80)),
            new GesturePoint(60, 60, now.AddMilliseconds(160)),
            new GesturePoint(0, 60, now.AddMilliseconds(240))
        ]);

        Assert.Equal("RDL", viewModel.NewGesturePattern);
        Assert.Contains("识别为", viewModel.RecordGestureStatusText, StringComparison.Ordinal);
    }

    [Fact]
    public void Recording_short_custom_gesture_keeps_existing_pattern_and_shows_hint()
    {
        var viewModel = CreateViewModel();
        viewModel.NewGesturePattern = "DR";
        var now = DateTimeOffset.UtcNow;

        viewModel.SetNewGesturePatternFromRecordedPoints(
        [
            new GesturePoint(0, 0, now),
            new GesturePoint(2, 2, now.AddMilliseconds(50))
        ]);

        Assert.Equal("DR", viewModel.NewGesturePattern);
        Assert.Equal("没识别出来，画得稍微长一点。", viewModel.RecordGestureStatusText);
    }

    [Fact]
    public async Task Changing_gesture_binding_saves_custom_preset()
    {
        var settings = new FakeSettingsService();
        var viewModel = CreateViewModel(settings: settings);
        var upCard = viewModel.GestureBindingCards.Single(card => card.Pattern == "U");

        upCard.SelectedAction = BuiltInGestureAction.OpenClipboardOverlay;

        await WaitForAsync(() =>
            settings.Values.TryGetValue(SettingKeys.GesturePreset, out var preset) && Equals(preset, GesturePreset.Custom) &&
            settings.Values.TryGetValue(SettingKeys.GestureCustomBindingsJson, out var json) &&
            json is string text && text.Contains("\"U\"", StringComparison.Ordinal));
        Assert.Equal(GesturePreset.Custom, viewModel.SelectedGesturePresetOption?.Value);
    }

    [Fact]
    public async Task Adding_custom_gesture_pattern_saves_binding()
    {
        var settings = new FakeSettingsService();
        var viewModel = CreateViewModel(settings: settings);

        viewModel.NewGesturePattern = "urdl";
        viewModel.NewGestureAction = BuiltInGestureAction.Enter;

        viewModel.AddCustomGestureBindingCommand.Execute(null);

        Assert.Contains(viewModel.GestureBindingCards, card => card.Pattern == "URDL" && card.SelectedAction == BuiltInGestureAction.Enter);
        await WaitForAsync(() =>
            settings.Values.TryGetValue(SettingKeys.GestureCustomBindingsJson, out var json) &&
            json is string text && text.Contains("\"URDL\"", StringComparison.Ordinal));
    }

    [Fact]
    public void Selecting_gesture_binding_updates_editor_card()
    {
        var viewModel = CreateViewModel();
        var card = viewModel.GestureBindingCards.Single(item => item.Pattern == "DR");

        viewModel.SelectedGestureBindingCard = card;

        Assert.Same(card, viewModel.SelectedGestureBindingCard);
        Assert.True(viewModel.HasSelectedGestureBinding);
        Assert.Equal("DR", viewModel.SelectedGestureBindingPattern);
        Assert.Equal(card.ActionName, viewModel.SelectedGestureBindingActionName);
    }

    [Fact]
    public async Task Deleting_selected_gesture_binding_selects_next_card()
    {
        var settings = new FakeSettingsService();
        var viewModel = CreateViewModel(settings: settings);
        var card = viewModel.GestureBindingCards.Single(item => item.Pattern == "DL");

        viewModel.SelectedGestureBindingCard = card;
        await viewModel.DeleteSelectedGestureBindingAsync();

        Assert.DoesNotContain(viewModel.GestureBindingCards, item => item.Pattern == "DL");
        Assert.NotNull(viewModel.SelectedGestureBindingCard);
        Assert.NotSame(card, viewModel.SelectedGestureBindingCard);
    }

    [Fact]
    public async Task Deleting_gesture_binding_saves_custom_preset()
    {
        var settings = new FakeSettingsService();
        var viewModel = CreateViewModel(settings: settings);
        var card = viewModel.GestureBindingCards.Single(item => item.Pattern == "DL");

        card.DeleteCommand.Execute(null);

        Assert.DoesNotContain(viewModel.GestureBindingCards, item => item.Pattern == "DL");
        await WaitForAsync(() =>
            settings.Values.TryGetValue(SettingKeys.GestureCustomBindingsJson, out var json) &&
            json is string text && !text.Contains("\"DL\"", StringComparison.Ordinal));
        Assert.Equal(GesturePreset.Custom, viewModel.SelectedGesturePresetOption?.Value);
    }

    [Fact]
    public async Task Changing_gesture_stroke_color_saves_setting()
    {
        var settings = new FakeSettingsService();
        var viewModel = CreateViewModel(settings: settings);

        viewModel.SelectedGestureStrokeColorOption = viewModel.GestureStrokeColorOptions.Single(option => option.Color == "#6EE7D8");

        await WaitForAsync(() =>
            settings.Values.TryGetValue(SettingKeys.GestureStrokeColor, out var color) &&
            Equals(color, "#6EE7D8"));
    }

    [Fact]
    public async Task Changing_extra_gesture_trigger_toggles_saves_settings()
    {
        var settings = new FakeSettingsService();
        var viewModel = CreateViewModel(settings: settings);

        viewModel.GestureMiddleButtonEnabled = false;
        viewModel.GestureXButton1Enabled = false;
        viewModel.GestureXButton2Enabled = false;
        viewModel.GestureLeftButtonEnabled = true;
        viewModel.GestureMiddleButtonEnabled = true;
        viewModel.GestureXButton1Enabled = true;
        viewModel.GestureXButton2Enabled = true;

        await WaitForAsync(() =>
            settings.Values.TryGetValue(SettingKeys.GestureTriggerLeftButtonEnabled, out var left) && Equals(left, true) &&
            settings.Values.TryGetValue(SettingKeys.GestureTriggerMiddleButtonEnabled, out var middle) && Equals(middle, true) &&
            settings.Values.TryGetValue(SettingKeys.GestureTriggerXButton1Enabled, out var x1) && Equals(x1, true) &&
            settings.Values.TryGetValue(SettingKeys.GestureTriggerXButton2Enabled, out var x2) && Equals(x2, true));
    }

    [Fact]
    public async Task Changing_edge_trigger_settings_saves_settings_and_starts_service()
    {
        var settings = new FakeSettingsService();
        var edge = new FakeEdgeTriggerService();
        var viewModel = CreateViewModel(settings: settings, edgeTriggerService: edge);

        viewModel.EdgeTriggerEnabled = false;
        viewModel.EdgeTriggerEnabled = true;
        viewModel.EdgeTriggerTopLeftAction = BuiltInGestureAction.ShowDesktop;
        viewModel.EdgeTriggerBottomRightAction = BuiltInGestureAction.StartMenu;

        await WaitForAsync(() =>
            edge.StartCount == 1 &&
            settings.Values.TryGetValue(SettingKeys.EdgeTriggerEnabled, out var enabled) && Equals(enabled, true) &&
            settings.Values.TryGetValue(SettingKeys.EdgeTriggerTopLeftAction, out var topLeft) && Equals(topLeft, BuiltInGestureAction.ShowDesktop) &&
            settings.Values.TryGetValue(SettingKeys.EdgeTriggerBottomRightAction, out var bottomRight) && Equals(bottomRight, BuiltInGestureAction.StartMenu));
    }

    [Fact]
    public async Task Changing_workstation_dashboard_settings_saves_settings()
    {
        var settings = new FakeSettingsService();
        var viewModel = CreateViewModel(settings: settings);

        viewModel.WorkstationEnabled = false;
        viewModel.WorkstationMonthlySalary = 18000m;
        viewModel.WorkstationWorkStartTime = "09:30";
        viewModel.WorkstationWorkEndTime = "18:30";
        viewModel.WorkstationLunchStartTime = "12:10";
        viewModel.WorkstationLunchEndTime = "13:20";
        viewModel.WorkstationWorkdays = "1,2,3,4";
        viewModel.WorkstationPayday = 8;
        viewModel.WorkstationShowFishingValue = false;
        viewModel.WorkstationShowOffWorkCountdown = false;
        viewModel.WorkstationDailyReportEnabled = true;

        await WaitForAsync(() =>
            settings.Values.TryGetValue(SettingKeys.WorkstationEnabled, out var enabled) && Equals(enabled, false) &&
            settings.Values.TryGetValue(SettingKeys.WorkstationMonthlySalary, out var salary) && Equals(salary, 18000m) &&
            settings.Values.TryGetValue(SettingKeys.WorkstationWorkStartTime, out var start) && Equals(start, "09:30") &&
            settings.Values.TryGetValue(SettingKeys.WorkstationWorkEndTime, out var end) && Equals(end, "18:30") &&
            settings.Values.TryGetValue(SettingKeys.WorkstationLunchStartTime, out var lunchStart) && Equals(lunchStart, "12:10") &&
            settings.Values.TryGetValue(SettingKeys.WorkstationLunchEndTime, out var lunchEnd) && Equals(lunchEnd, "13:20") &&
            settings.Values.TryGetValue(SettingKeys.WorkstationWorkdays, out var workdays) && Equals(workdays, "1,2,3,4") &&
            settings.Values.TryGetValue(SettingKeys.WorkstationPayday, out var payday) && Equals(payday, 8) &&
            settings.Values.TryGetValue(SettingKeys.WorkstationShowFishingValue, out var showFishing) && Equals(showFishing, false) &&
            settings.Values.TryGetValue(SettingKeys.WorkstationShowOffWorkCountdown, out var showCountdown) && Equals(showCountdown, false) &&
            settings.Values.TryGetValue(SettingKeys.WorkstationDailyReportEnabled, out var dailyReport) && Equals(dailyReport, true));
    }

    [Fact]
    public async Task Changing_overwork_reminder_settings_saves_clamped_values()
    {
        var settings = new FakeSettingsService();
        var viewModel = CreateViewModel(settings: settings);

        viewModel.WorkstationEnableOverworkReminder = false;
        viewModel.WorkstationOverworkReminderIntervalMinutes = 10;
        viewModel.WorkstationOverworkHighRiskAfterHours = 20;
        viewModel.WorkstationEnableHudTimeColor = false;
        viewModel.WorkstationEnableStrongOverworkWarning = true;
        viewModel.WorkstationOverworkReminderCanSnooze = false;
        viewModel.WorkstationOverworkSnoozeMinutes = 1;

        await WaitForAsync(() =>
            settings.Values.TryGetValue(SettingKeys.WorkstationEnableOverworkReminder, out var enabled) && Equals(enabled, false) &&
            settings.Values.TryGetValue(SettingKeys.WorkstationOverworkReminderIntervalMinutes, out var interval) && Equals(interval, 30) &&
            settings.Values.TryGetValue(SettingKeys.WorkstationOverworkHighRiskAfterHours, out var highRisk) && Equals(highRisk, 12d) &&
            settings.Values.TryGetValue(SettingKeys.WorkstationEnableHudTimeColor, out var hudColor) && Equals(hudColor, false) &&
            settings.Values.TryGetValue(SettingKeys.WorkstationEnableStrongOverworkWarning, out var strong) && Equals(strong, true) &&
            settings.Values.TryGetValue(SettingKeys.WorkstationOverworkReminderCanSnooze, out var canSnooze) && Equals(canSnooze, false) &&
            settings.Values.TryGetValue(SettingKeys.WorkstationOverworkSnoozeMinutes, out var snooze) && Equals(snooze, 5));
    }

    [Fact]
    public void Overwork_reminder_preview_uses_plain_local_text()
    {
        var viewModel = CreateViewModel();

        Assert.Contains("当前阶段", viewModel.OverworkPreviewStageText);
        Assert.Contains("HUD 颜色", viewModel.OverworkPreviewHudColorText);
        Assert.Contains("下次提醒", viewModel.OverworkPreviewNextReminderText);
        Assert.Contains("今日连续工作", viewModel.OverworkPreviewWorkedText);
    }

    [Fact]
    public void Edge_trigger_defaults_are_responsive()
    {
        var viewModel = CreateViewModel();

        Assert.Equal(160, viewModel.EdgeTriggerDwellMs);
        Assert.Equal(450, viewModel.EdgeTriggerCooldownMs);
        Assert.Equal(56, viewModel.EdgeTriggerSlideThreshold);
    }

    [Fact]
    public async Task Worker_level_settings_are_visible_and_saved()
    {
        var settings = new FakeSettingsService();
        settings.Values[SettingKeys.WorkerLevelShowLevelUpPopup] = true;
        settings.Values[SettingKeys.WorkerLevelShowLevelInHud] = true;
        settings.Values[SettingKeys.HudFunTextEnabled] = true;
        settings.Values[SettingKeys.HudStatusLevelEnabled] = true;
        var worker = new FakeWorkerLevelService();
        var viewModel = CreateViewModel(settings: settings, workerLevel: worker);

        await WaitForAsync(() => viewModel.WorkerLevelText.Contains("Lv.3", StringComparison.Ordinal));
        viewModel.WorkerLevelShowLevelUpPopup = false;
        viewModel.WorkerLevelShowLevelInHud = false;
        viewModel.HudFunTextEnabled = false;
        viewModel.HudStatusLevelEnabled = false;

        await WaitForAsync(() =>
            settings.Values.TryGetValue(SettingKeys.WorkerLevelShowLevelUpPopup, out var popup) && Equals(popup, false) &&
            settings.Values.TryGetValue(SettingKeys.WorkerLevelShowLevelInHud, out var level) && Equals(level, false) &&
            settings.Values.TryGetValue(SettingKeys.HudFunTextEnabled, out var fun) && Equals(fun, false) &&
            settings.Values.TryGetValue(SettingKeys.HudStatusLevelEnabled, out var status) && Equals(status, false));
        Assert.Equal("XP 128 / 250", viewModel.WorkerXpText);
    }

    [Fact]
    public void Workstation_preview_updates_from_salary_and_payday_settings()
    {
        var viewModel = CreateViewModel();

        viewModel.WorkstationMonthlySalary = 21750m;
        viewModel.WorkstationPayday = 20;

        Assert.Contains("今天已赚", viewModel.WorkstationPreviewTodayEarnedText);
        Assert.Contains("¥", viewModel.WorkstationPreviewTodayEarnedText);
        Assert.Contains("距离下班", viewModel.WorkstationPreviewOffWorkText);
        Assert.Contains("发薪", viewModel.WorkstationPreviewPaydayText);
        Assert.False(string.IsNullOrWhiteSpace(viewModel.WorkstationPreviewStatusText));
    }

    [Fact]
    public void Workstation_template_command_fills_plain_work_rules()
    {
        var viewModel = CreateViewModel();
        var template = viewModel.WorkstationTemplateOptions.Single(item => item.Name == "标准双休 09:00-18:00");

        viewModel.ApplyWorkstationTemplateCommand.Execute(template);

        Assert.Equal("09:00", viewModel.WorkstationWorkStartTime);
        Assert.Equal("18:00", viewModel.WorkstationWorkEndTime);
        Assert.Equal("12:00", viewModel.WorkstationLunchStartTime);
        Assert.Equal("13:00", viewModel.WorkstationLunchEndTime);
        Assert.Equal("1,2,3,4,5", viewModel.WorkstationWorkdays);
    }
    private static SettingsViewModel CreateViewModel(
        FakeClipboardRepository? repository = null,
        FakeClipboardOverlayService? overlay = null,
        FakeConfirmationService? confirmation = null,
        FakeSettingsService? settings = null,
        FakeEdgeTriggerService? edgeTriggerService = null,
        FakeGlobalHotkeyService? hotkey = null,
        FakeWorkerLevelService? workerLevel = null)
    {
        settings ??= new FakeSettingsService();
        return new SettingsViewModel(
            new AppPathProvider(Path.Combine(Path.GetTempPath(), "gestureclip-settings-tests")),
            new FakePermissionService(),
            settings,
            new FakeMouseGestureService(),
            new FakeGestureSettingsProvider(),
            new FakeFeatureToggleService(),
            hotkey ?? new FakeGlobalHotkeyService(),
            new FakeAppBlacklistService(),
            new FakeStartupService(),
            new FakeDiagnosticsService(),
            new FakeClipboardService(),
            new FakeClipboardWriter(),
            repository ?? new FakeClipboardRepository(),
            overlay ?? new FakeClipboardOverlayService(),
            confirmation ?? new FakeConfirmationService { Result = true },
            new GestureClip.Features.Gestures.GesturePresetProvider(),
            edgeTriggerService ?? new FakeEdgeTriggerService(),
            workerLevel ?? new FakeWorkerLevelService());
    }

    private static async Task WaitForAsync(Func<bool> condition)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(2);
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(20);
        }

        Assert.True(condition());
    }

    private sealed class FakeWorkerLevelService : IWorkerLevelService
    {
        public Task<Core.WorkerLevel.WorkerLevelSnapshot> GetSnapshotAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(new Core.WorkerLevel.WorkerLevelSnapshot(
                128,
                10,
                new Core.WorkerLevel.WorkerLevelDefinition(3, 120, "粘贴熟练工"),
                new Core.WorkerLevel.WorkerLevelDefinition(4, 250, "摸鱼见习生"),
                8,
                130,
                0.06,
                false,
                3,
                null));
        }

        public Task<Core.WorkerLevel.WorkerLevelSnapshot> RecordActionAsync(BuiltInGestureAction action, bool isGestureSuccess, DateTimeOffset now, CancellationToken cancellationToken)
        {
            return GetSnapshotAsync(cancellationToken);
        }
    }
    private sealed class FakeClipboardRepository : IClipboardRepository
    {
        public int Count { get; set; }
        public int ClearAllCount { get; private set; }
        public int ClearUnpinnedCount { get; private set; }
        public (int MaxItems, int RetentionDays)? LastCleanup { get; private set; }

        public Task<int> GetCountAsync(CancellationToken cancellationToken) => Task.FromResult(Count);
        public Task<int> ClearAllAsync(CancellationToken cancellationToken)
        {
            ClearAllCount++;
            var deleted = Count;
            Count = 0;
            return Task.FromResult(deleted);
        }

        public Task<int> ClearUnpinnedAsync(CancellationToken cancellationToken)
        {
            ClearUnpinnedCount++;
            Count = Math.Min(1, Count);
            return Task.FromResult(0);
        }

        public Task<int> CleanupAsync(int maxItems, int retentionDays, CancellationToken cancellationToken)
        {
            LastCleanup = (maxItems, retentionDays);
            Count = Math.Min(Count, Math.Max(maxItems, 0));
            return Task.FromResult(0);
        }

        public Task<ClipboardItem?> FindByHashAsync(string hash, CancellationToken cancellationToken) => Task.FromResult<ClipboardItem?>(null);
        public Task InsertAsync(ClipboardItem item, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<IReadOnlyList<ClipboardItem>> SearchAsync(string keyword, int limit, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<ClipboardItem>>([]);
        public Task<ClipboardItem?> GetLatestAsync(CancellationToken cancellationToken) => Task.FromResult<ClipboardItem?>(null);
        public Task IncrementUseCountAsync(Guid id, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<int> DeleteAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken) => Task.FromResult(ids.Count);
        public Task SetPinnedAsync(Guid id, bool isPinned, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task SetFavoriteAsync(Guid id, bool isFavorite, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<bool> IsProcessBlockedAsync(string? processName, CancellationToken cancellationToken) => Task.FromResult(false);
    }

    private sealed class FakeConfirmationService : IConfirmationService
    {
        public bool Result { get; set; }
        public List<(string Title, string Message)> Prompts { get; } = [];
        public bool Confirm(string title, string message)
        {
            Prompts.Add((title, message));
            return Result;
        }
    }

    private sealed class FakeClipboardOverlayService : IClipboardOverlayService
    {
        public int RefreshCount { get; private set; }
        public Task ShowAsync() => Task.CompletedTask;
        public Task ToggleAsync() => Task.CompletedTask;
        public Task RefreshAsync()
        {
            RefreshCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeSettingsService : ISettingsService
    {
        public Dictionary<string, object?> Values { get; } = new();
        public T Get<T>(string key, T defaultValue) => Values.TryGetValue(key, out var value) ? (T)value! : defaultValue;
        public Task SetAsync<T>(string key, T value, CancellationToken cancellationToken)
        {
            Values[key] = value;
            return Task.CompletedTask;
        }
    }

    private sealed class FakePermissionService : ISystemPermissionService
    {
        public PermissionStatus GetCurrentStatus() => PermissionStatus.Normal;
    }

    private sealed class FakeMouseGestureService : IMouseGestureService
    {
        public bool IsEnabled => true;
        public GestureDiagnosticsSnapshot Diagnostics => new("已安装", GestureRuntimeState.Idle, null, BuiltInGestureAction.None, null, null, false);
        public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakeEdgeTriggerService : IEdgeTriggerService
    {
        public bool IsEnabled { get; private set; }
        public int StartCount { get; private set; }
        public int StopCount { get; private set; }
        public EdgeTriggerDiagnosticsSnapshot Diagnostics => new(IsEnabled, "测试", "1, 2", BuiltInGestureAction.ShowDesktop, "已触发", DateTimeOffset.UtcNow, null);

        public Task StartAsync(CancellationToken cancellationToken)
        {
            IsEnabled = true;
            StartCount++;
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            IsEnabled = false;
            StopCount++;
            return Task.CompletedTask;
        }

        public void RefreshSettings() { }
    }

    private sealed class FakeGestureSettingsProvider : IGestureSettingsProvider
    {
        public GestureSettings GetCurrent() => new(true, true, false, false, GesturePreset.EditEnhanced, new GestureOptions(20, 16, 2000, 2));
        public void Update(GestureSettings settings) { }
    }

    private sealed class FakeFeatureToggleService : IFeatureToggleService
    {
        public FeatureToggleSnapshot GetSnapshot() => new(true, true);
        public Task SetClipboardCaptureEnabledAsync(bool enabled, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task SetGestureEnabledAsync(bool enabled, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task ToggleClipboardCaptureAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task ToggleGestureAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakeGlobalHotkeyService : IGlobalHotkeyService
    {
        public HotkeyStatus Status { get; } = new(HotkeyRegistrationState.Registered, "Ctrl + ` 已注册");
        public int StartCount { get; private set; }
        public int StopCount { get; private set; }
        public void Start() => StartCount++;
        public void Stop() => StopCount++;
    }

    private sealed class FakeAppBlacklistService : IAppBlacklistService
    {
        public Task<IReadOnlyList<AppBlacklistItem>> GetAllAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<AppBlacklistItem>>([]);
        public Task AddAsync(string processName, bool blockClipboard, bool blockGesture, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task DeleteAsync(Guid id, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task UpdateAsync(Guid id, bool blockClipboard, bool blockGesture, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<bool> IsClipboardBlockedAsync(string? processName, CancellationToken cancellationToken) => Task.FromResult(false);
        public Task<bool> IsGestureBlockedAsync(string? processName, CancellationToken cancellationToken) => Task.FromResult(false);
        public Task RefreshAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public bool IsGestureBlockedCached(string? processName) => false;
    }

    private sealed class FakeStartupService : IStartupService
    {
        public bool IsEnabled() => false;
        public void Enable() { }
        public void Disable() { }
        public string GetStartupCommand() => "";
        public bool IsDevelopmentRunMode() => false;
    }

    private sealed class FakeDiagnosticsService : IDiagnosticsService
    {
        public Task<DiagnosticsSnapshot> GetSnapshotAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(new DiagnosticsSnapshot("1.0", "app.exe", "db", "logs", false, true, true, "hotkey", "hook", null, null, null, null));
        }

        public Task<string> BuildReportAsync(CancellationToken cancellationToken) => Task.FromResult("diagnostics");
    }

    private sealed class FakeClipboardService : IClipboardService
    {
        public bool IsCaptureEnabled => true;
        public DateTimeOffset? SuppressCaptureUntil => null;
        public void SuppressCaptureFor(TimeSpan duration) { }
        public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task SetCaptureEnabledAsync(bool enabled, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task CaptureTextAsync(ClipboardCapture capture, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<IReadOnlyList<ClipboardItem>> SearchAsync(string keyword, int limit, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<ClipboardItem>>([]);
        public Task<ClipboardItem?> GetLatestAsync(CancellationToken cancellationToken) => Task.FromResult<ClipboardItem?>(null);
        public Task PasteAsync(ClipboardItem item, PasteOptions options, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task CopyItemsAsync(IReadOnlyList<ClipboardItem> items, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<int> DeleteItemsAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken) => Task.FromResult(ids.Count);
        public Task SetPinnedAsync(Guid id, bool isPinned, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task SetFavoriteAsync(Guid id, bool isFavorite, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakeClipboardWriter : IClipboardWriter
    {
        public Task SetTextAsync(string text, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task SetImagePngBase64Async(string pngBase64, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task SendPasteHotkeyAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}






