using GestureClip.App.ViewModels;
using GestureClip.Core.Abstractions;
using GestureClip.Core.Settings;
using GestureClip.Core.Workstation;
using Xunit;

namespace GestureClip.Tests.Workstation;

public sealed class WorkstationDashboardViewModelTests
{
    [Fact]
    public async Task RefreshAsync_updates_dashboard_fields()
    {
        var service = new FakeWorkstationDashboardService();
        service.Snapshot = Snapshot with
        {
            TimeUntilOffWork = TimeSpan.FromHours(2) + TimeSpan.FromMinutes(5),
            TodayEarned = 128.35m,
            MonthEarned = 3900.10m,
            DaysUntilPayday = 6,
            CopyCount = 7,
            PasteCount = 4,
            GestureCount = 3,
            EstimatedSavedClicks = 9,
            WorkStatusText = "低功耗运行期"
        };
        var viewModel = new WorkstationDashboardViewModel(service);

        await viewModel.RefreshAsync();

        Assert.Equal("02:05:00", viewModel.OffWorkCountdownText);
        Assert.Equal("￥128.35", viewModel.TodayEarnedText);
        Assert.Equal("￥3900.10", viewModel.MonthEarnedText);
        Assert.Equal("还剩 6 天", viewModel.PaydayText);
        Assert.Equal("复制 7 · 粘贴 4 · 手势 3 · 打开剪贴板 0", viewModel.ActionStatsText);
        Assert.Equal("大约省了 9 次点击", viewModel.SavedClicksText);
        Assert.Equal("低功耗运行期", viewModel.WorkStatusText);
    }

    [Fact]
    public async Task Start_and_end_fishing_call_service_and_refresh()
    {
        var service = new FakeWorkstationDashboardService();
        var viewModel = new WorkstationDashboardViewModel(service);

        await viewModel.StartFishingAsync();
        await viewModel.EndFishingAsync();

        Assert.Equal(1, service.StartFishingCount);
        Assert.Equal(1, service.EndFishingCount);
        Assert.True(service.RefreshCount >= 2);
    }

    [Fact]
    public async Task ResetTodayAsync_calls_service_for_today_and_refreshes()
    {
        var service = new FakeWorkstationDashboardService();
        var viewModel = new WorkstationDashboardViewModel(service);

        await viewModel.ResetTodayAsync();

        Assert.Equal(DateOnly.FromDateTime(DateTime.Today), service.LastResetDate);
        Assert.Equal(1, service.ResetCount);
        Assert.True(service.RefreshCount >= 1);
    }

    [Fact]
    public async Task RefreshAsync_hides_optional_values_when_settings_disable_them()
    {
        var service = new FakeWorkstationDashboardService
        {
            Snapshot = Snapshot with
            {
                TimeUntilOffWork = TimeSpan.FromHours(1),
                CurrentFishingValue = 12.3m,
                TodayFishingValue = 45.6m
            }
        };
        var settings = new FakeSettingsService();
        settings.Values[SettingKeys.WorkstationShowOffWorkCountdown] = false;
        settings.Values[SettingKeys.WorkstationShowFishingValue] = false;
        var viewModel = new WorkstationDashboardViewModel(service, settings);

        await viewModel.RefreshAsync();

        Assert.Equal("已隐藏", viewModel.OffWorkCountdownText);
        Assert.Equal("已隐藏", viewModel.CurrentFishingValueText);
        Assert.Equal("已隐藏", viewModel.TodayFishingValueText);
    }

    [Fact]
    public async Task RefreshAsync_shows_off_work_protection_hint()
    {
        var service = new FakeWorkstationDashboardService
        {
            Snapshot = Snapshot with
            {
                TimeUntilOffWork = TimeSpan.FromMinutes(25),
                WorkStatusText = "禁止新增需求期"
            }
        };
        var viewModel = new WorkstationDashboardViewModel(service);

        await viewModel.RefreshAsync();

        Assert.Equal("当前未进入下班冲刺。", viewModel.ProtectionHintText);
        Assert.False(string.IsNullOrWhiteSpace(viewModel.WorkTipText));
    }

    [Fact]
    public void NeedsSetup_is_true_when_salary_missing_and_setup_not_completed()
    {
        var settings = new FakeSettingsService();
        settings.Values[SettingKeys.WorkstationMonthlySalary] = 0m;
        settings.Values[SettingKeys.WorkBearSetupCompleted] = false;
        var viewModel = new WorkstationDashboardViewModel(new FakeWorkstationDashboardService(), settings);

        Assert.True(viewModel.NeedsSetup);
    }

    [Fact]
    public async Task CompleteSetupAsync_saves_salary_hours_and_clears_setup_banner()
    {
        var settings = new FakeSettingsService();
        settings.Values[SettingKeys.WorkstationMonthlySalary] = 0m;
        settings.Values[SettingKeys.WorkBearSetupCompleted] = false;
        var viewModel = new WorkstationDashboardViewModel(new FakeWorkstationDashboardService(), settings)
        {
            SetupSalaryText = "12000",
            SetupStartTime = "09:30",
            SetupEndTime = "18:30"
        };

        await viewModel.CompleteSetupAsync();

        Assert.Equal(12000m, settings.Values[SettingKeys.WorkstationMonthlySalary]);
        Assert.Equal("09:30", settings.Values[SettingKeys.WorkstationWorkStartTime]);
        Assert.Equal("18:30", settings.Values[SettingKeys.WorkstationWorkEndTime]);
        Assert.Equal(true, settings.Values[SettingKeys.WorkBearSetupCompleted]);
        Assert.False(viewModel.NeedsSetup);
        Assert.Contains("配置完成", viewModel.LastMessage);
    }

    [Fact]
    public async Task DismissSetupAsync_marks_setup_completed_without_salary()
    {
        var settings = new FakeSettingsService();
        settings.Values[SettingKeys.WorkBearSetupCompleted] = false;
        var viewModel = new WorkstationDashboardViewModel(new FakeWorkstationDashboardService(), settings);

        await viewModel.DismissSetupAsync();

        Assert.Equal(true, settings.Values[SettingKeys.WorkBearSetupCompleted]);
        Assert.False(viewModel.NeedsSetup);
    }


    [Fact]
    public async Task ResetTodayAsync_cancels_when_user_declines_confirmation()
    {
        var service = new FakeWorkstationDashboardService();
        var confirm = new FakeConfirmationService { Result = false };
        var viewModel = new WorkstationDashboardViewModel(service, confirmationService: confirm);

        await viewModel.ResetTodayAsync();

        Assert.Equal(0, service.ResetCount);
        Assert.Equal(1, confirm.CallCount);
        Assert.Contains("取消", viewModel.LastMessage);
    }

    [Fact]
    public async Task ResetTodayAsync_resets_when_user_confirms()
    {
        var service = new FakeWorkstationDashboardService();
        var confirm = new FakeConfirmationService { Result = true };
        var viewModel = new WorkstationDashboardViewModel(service, confirmationService: confirm);

        await viewModel.ResetTodayAsync();

        Assert.Equal(1, service.ResetCount);
        Assert.Equal(1, confirm.CallCount);
        Assert.Contains("已重置", viewModel.LastMessage);
    }

    [Fact]
    public void ShowEmptySalaryGuide_true_when_setup_done_but_salary_missing()
    {
        var settings = new FakeSettingsService();
        settings.Values[SettingKeys.WorkstationMonthlySalary] = 0m;
        settings.Values[SettingKeys.WorkBearSetupCompleted] = true;
        var viewModel = new WorkstationDashboardViewModel(new FakeWorkstationDashboardService(), settings);

        Assert.False(viewModel.NeedsSetup);
        Assert.True(viewModel.ShowEmptySalaryGuide);
    }

    [Fact]
    public void OpenWorkRules_expands_settings_section()
    {
        var settings = new FakeSettingsService();
        settings.Values[SettingKeys.WorkstationMonthlySalary] = 0m;
        settings.Values[SettingKeys.WorkBearSetupCompleted] = true;
        var viewModel = new WorkstationDashboardViewModel(new FakeWorkstationDashboardService(), settings);

        viewModel.OpenWorkRules();

        Assert.True(viewModel.SettingsExpanded);
        Assert.Contains("月薪", viewModel.LastMessage);
    }

    private static readonly WorkstationDashboardSnapshot Snapshot = new(
        "工位小熊",
        "今天也在低功耗运行",
        TimeSpan.Zero,
        0m,
        0m,
        0,
        false,
        TimeSpan.Zero,
        0m,
        0m,
        0,
        0,
        0,
        0,
        "开机缓冲期");

    private sealed class FakeWorkstationDashboardService : IWorkstationDashboardService
    {
        public WorkstationDashboardSnapshot Snapshot { get; set; } = WorkstationDashboardViewModelTests.Snapshot;
        public int RefreshCount { get; private set; }
        public int StartFishingCount { get; private set; }
        public int EndFishingCount { get; private set; }
        public int ResetCount { get; private set; }
        public DateOnly? LastResetDate { get; private set; }

        public Task<WorkstationDashboardSnapshot> GetSnapshotAsync(DateTimeOffset now, CancellationToken cancellationToken)
        {
            RefreshCount++;
            return Task.FromResult(Snapshot);
        }

        public Task StartFishingAsync(DateTimeOffset now, CancellationToken cancellationToken)
        {
            StartFishingCount++;
            return Task.CompletedTask;
        }

        public Task EndFishingAsync(DateTimeOffset now, CancellationToken cancellationToken)
        {
            EndFishingCount++;
            return Task.CompletedTask;
        }

        public Task ResetTodayAsync(DateOnly date, CancellationToken cancellationToken)
        {
            LastResetDate = date;
            ResetCount++;
            return Task.CompletedTask;
        }

        public Task RecordCopyAsync(DateTimeOffset now, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task RecordPasteAsync(DateTimeOffset now, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task RecordGestureAsync(DateTimeOffset now, CancellationToken cancellationToken) => Task.CompletedTask;
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

    private sealed class FakeConfirmationService : IConfirmationService
    {
        public bool Result { get; set; } = true;
        public int CallCount { get; private set; }
        public string? LastTitle { get; private set; }
        public string? LastMessageText { get; private set; }

        public bool Confirm(string title, string message)
        {
            CallCount++;
            LastTitle = title;
            LastMessageText = message;
            return Result;
        }
    }
}
