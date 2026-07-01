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
    public void Gesture_binding_cards_are_created_for_supported_patterns()
    {
        var viewModel = CreateViewModel();

        Assert.Equal(8, viewModel.GestureBindingCards.Count);
        Assert.Contains(viewModel.GestureBindingCards, card => card.Pattern == "U" && card.SelectedAction == BuiltInGestureAction.Copy);
        Assert.Contains(viewModel.GestureBindingCards, card => card.Pattern == "D" && card.SelectedAction == BuiltInGestureAction.Paste);
    }

    [Fact]
    public void Gesture_trigger_modes_include_side_and_edge_placeholders()
    {
        var viewModel = CreateViewModel();

        Assert.Contains(viewModel.GestureTriggerModes, mode => mode.Name == "鼠标右键" && mode.IsEnabled);
        Assert.Contains(viewModel.GestureTriggerModes, mode => mode.Name == "鼠标侧键 1" && !mode.IsEnabled);
        Assert.Contains(viewModel.GestureTriggerModes, mode => mode.Name == "屏幕左边缘 + 鼠标中键" && !mode.IsEnabled);
        Assert.Contains(viewModel.GestureTriggerModes, mode => mode.Name == "屏幕右上角 + 滚轮" && !mode.IsEnabled);
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

        viewModel.GestureMiddleButtonEnabled = true;
        viewModel.GestureXButton1Enabled = true;
        viewModel.GestureXButton2Enabled = true;

        await WaitForAsync(() =>
            settings.Values.TryGetValue(SettingKeys.GestureTriggerMiddleButtonEnabled, out var middle) && Equals(middle, true) &&
            settings.Values.TryGetValue(SettingKeys.GestureTriggerXButton1Enabled, out var x1) && Equals(x1, true) &&
            settings.Values.TryGetValue(SettingKeys.GestureTriggerXButton2Enabled, out var x2) && Equals(x2, true));
    }

    private static SettingsViewModel CreateViewModel(
        FakeClipboardRepository? repository = null,
        FakeClipboardOverlayService? overlay = null,
        FakeConfirmationService? confirmation = null,
        FakeSettingsService? settings = null)
    {
        settings ??= new FakeSettingsService();
        return new SettingsViewModel(
            new AppPathProvider(Path.Combine(Path.GetTempPath(), "gestureclip-settings-tests")),
            new FakePermissionService(),
            settings,
            new FakeMouseGestureService(),
            new FakeGestureSettingsProvider(),
            new FakeFeatureToggleService(),
            new FakeGlobalHotkeyService(),
            new FakeAppBlacklistService(),
            new FakeStartupService(),
            new FakeDiagnosticsService(),
            new FakeClipboardService(),
            new FakeClipboardWriter(),
            repository ?? new FakeClipboardRepository(),
            overlay ?? new FakeClipboardOverlayService(),
            confirmation ?? new FakeConfirmationService { Result = true },
            new GestureClip.Features.Gestures.GesturePresetProvider());
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
        public HotkeyStatus Status { get; } = new(HotkeyRegistrationState.Registered, "Ctrl+Alt+V 已注册");
        public void Start() { }
        public void Stop() { }
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
    }

    private sealed class FakeClipboardWriter : IClipboardWriter
    {
        public Task SetTextAsync(string text, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task SendPasteHotkeyAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
