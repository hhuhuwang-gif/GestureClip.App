using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using GestureClip.Core.Abstractions;
using GestureClip.Core.Settings;
using GestureClip.Core.Workstation;

namespace GestureClip.App.ViewModels;

public sealed class WorkstationDashboardViewModel : INotifyPropertyChanged
{
    private readonly IWorkstationDashboardService _dashboardService;
    private readonly ISettingsService? _settingsService;
    private readonly IWorkBearShareCardService? _shareCardService;
    private readonly IOverworkReminderService? _overworkReminderService;
    private WorkstationDashboardSnapshot _snapshot = EmptySnapshot;
    private string _lastMessage = "本地统计，不上传，不记录剪贴板正文。";
    private bool _settingsExpanded;

    public WorkstationDashboardViewModel(
        IWorkstationDashboardService dashboardService,
        ISettingsService? settingsService = null,
        IWorkBearShareCardService? shareCardService = null,
        IOverworkReminderService? overworkReminderService = null)
    {
        _dashboardService = dashboardService;
        _settingsService = settingsService;
        _shareCardService = shareCardService;
        _overworkReminderService = overworkReminderService;
        RefreshCommand = new AsyncRelayCommand(_ => RefreshAsync());
        StartFishingCommand = new AsyncRelayCommand(_ => StartFishingAsync());
        EndFishingCommand = new AsyncRelayCommand(_ => EndFishingAsync());
        ToggleFishingCommand = new AsyncRelayCommand(_ => ToggleFishingAsync());
        ClearFishingCommand = new AsyncRelayCommand(_ => ClearFishingAsync());
        ResetTodayCommand = new AsyncRelayCommand(_ => ResetTodayAsync());
        GenerateReportCommand = new AsyncRelayCommand(_ => GenerateReportAsync());
        GenerateShareCardCommand = new AsyncRelayCommand(_ => GenerateShareCardAsync(), _ => _shareCardService is not null);
        StartSprintCommand = new AsyncRelayCommand(_ => SetSprintAsync(true));
        StopSprintCommand = new AsyncRelayCommand(_ => SetSprintAsync(false));
        ToggleRestReminderCommand = new AsyncRelayCommand(_ => ToggleRestReminderAsync());
        SnoozeRestReminderCommand = new AsyncRelayCommand(_ => SnoozeRestReminderAsync());
        MuteRestReminderTodayCommand = new AsyncRelayCommand(_ => MuteRestReminderTodayAsync());
        DisableRestReminderCommand = new AsyncRelayCommand(_ => DisableRestReminderAsync());
        MuteDailyReportTodayCommand = new AsyncRelayCommand(_ => MuteDailyReportTodayAsync());
        ToggleAutoDailyReportCommand = new AsyncRelayCommand(_ => ToggleAutoDailyReportAsync());
        ApplyWorkTemplateCommand = new RelayCommand(ApplyWorkTemplate);
        ToggleSettingsCommand = new RelayCommand(_ => SettingsExpanded = !SettingsExpanded);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ICommand RefreshCommand { get; }
    public ICommand StartFishingCommand { get; }
    public ICommand EndFishingCommand { get; }
    public ICommand ToggleFishingCommand { get; }
    public ICommand ClearFishingCommand { get; }
    public ICommand ResetTodayCommand { get; }
    public ICommand GenerateReportCommand { get; }
    public ICommand GenerateShareCardCommand { get; }
    public ICommand StartSprintCommand { get; }
    public ICommand StopSprintCommand { get; }
    public ICommand ToggleRestReminderCommand { get; }
    public ICommand SnoozeRestReminderCommand { get; }
    public ICommand MuteRestReminderTodayCommand { get; }
    public ICommand DisableRestReminderCommand { get; }
    public ICommand MuteDailyReportTodayCommand { get; }
    public ICommand ToggleAutoDailyReportCommand { get; }
    public ICommand ApplyWorkTemplateCommand { get; }
    public ICommand ToggleSettingsCommand { get; }

    public string Title => _snapshot.Title;
    public string Subtitle => _snapshot.Subtitle;
    public string CurrentStateText => _snapshot.WorkStageText;
    public string WorkStatusText => _snapshot.WorkStatusText;
    public string BearStatusText => _snapshot.BearStatusText;
    public string BearLineText => _snapshot.BearLineText;
    public string OffWorkCountdownText => ShowOffWorkCountdown ? FormatDurationClock(_snapshot.TimeUntilOffWork) : "已隐藏";
    public string TodayEarnedText => FormatMoney(_snapshot.TodayEarned);
    public string MonthEarnedText => FormatMoney(_snapshot.MonthEarned);
    public string MinuteValueText => FormatMoney(_snapshot.MinuteValue);
    public string BossPaidText => FormatMoney(_snapshot.TodayEarned);
    public string PaydayText => $"{_snapshot.DaysUntilPayday} 天";
    public string FishingButtonText => _snapshot.IsFishing ? "结束摸鱼" : "开始摸鱼";
    public string CurrentFishingText => FormatDuration(_snapshot.CurrentFishingDuration);
    public string CurrentFishingValueText => ShowFishingValue ? FormatMoney(_snapshot.CurrentFishingValue) : "已隐藏";
    public string TodayFishingDurationText => FormatDuration(_snapshot.TodayFishingDuration);
    public string TodayFishingValueText => ShowFishingValue ? FormatMoney(_snapshot.TodayFishingValue) : "已隐藏";
    public string CopyCountText => _snapshot.CopyCount.ToString();
    public string PasteCountText => _snapshot.PasteCount.ToString();
    public string GestureCountText => _snapshot.GestureCount.ToString();
    public string SavedClicksText => $"少点 {_snapshot.EstimatedSavedClicks} 次";
    public string OpenClipboardCountText => _snapshot.OpenClipboardCount.ToString();
    public string ActionStatsText => $"复制 {_snapshot.CopyCount} · 粘贴 {_snapshot.PasteCount} · 手势 {_snapshot.GestureCount} · 剪贴板 {_snapshot.OpenClipboardCount}";
    public string ProtectionHintText => _snapshot.SprintActive ? "已进入下班冲刺，建议保存文件、整理明天待办、准备撤退。" : "当前未进入下班冲刺。";
    public string WorkTipText => _snapshot.BearLineText;
    public string ContinuousWorkText => FormatDuration(_snapshot.ContinuousWorkDuration);
    public string NextRestReminderText => _snapshot.NextRestReminderAt is null ? "已关闭" : _snapshot.NextRestReminderAt.Value.ToLocalTime().ToString("HH:mm");
    public string RestReminderCountText => $"今日已提醒 {_snapshot.RestReminderCount} 次";
    public string RestRiskText => _snapshot.RestRiskText;
    public string RestReminderButtonText => _snapshot.RestReminderEnabled ? "今天不再提醒" : "开启提醒";
    public string AutoDailyReportButtonText => AutoShowDailyReport ? "关闭自动报告" : "开启自动报告";
    public string SprintCountdownText => _snapshot.TimeUntilOffWork > TimeSpan.Zero ? FormatDuration(_snapshot.TimeUntilOffWork) : "已下班";
    public string SprintSuggestionText => _snapshot.SprintSuggestionText;
    public string DailyRatingText => _snapshot.DailyRatingText;
    public string DailyReportText => _snapshot.DailyReportText;
    public string SalaryHintText => _snapshot.SalaryHintText;
    public bool AutoShowDailyReport => _settingsService?.Get(SettingKeys.AutoShowDailyWorkReport, true) ?? true;
    public string LastMessage
    {
        get => _lastMessage;
        private set
        {
            if (_lastMessage == value) return;
            _lastMessage = value;
            OnPropertyChanged();
        }
    }

    public bool SettingsExpanded
    {
        get => _settingsExpanded;
        set
        {
            if (_settingsExpanded == value) return;
            _settingsExpanded = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(SettingsToggleText));
        }
    }

    public string SettingsToggleText => SettingsExpanded ? "收起工作规则设置" : "展开工作规则设置";

    public decimal WorkstationMonthlySalary
    {
        get => _settingsService?.Get(SettingKeys.WorkstationMonthlySalary, 0m) ?? 0m;
        set => _ = SetSettingAsync(SettingKeys.WorkstationMonthlySalary, value);
    }

    public string WorkstationWorkStartTime
    {
        get => _settingsService?.Get(SettingKeys.WorkstationWorkStartTime, "09:00") ?? "09:00";
        set => _ = SetSettingAsync(SettingKeys.WorkstationWorkStartTime, value);
    }

    public string WorkstationWorkEndTime
    {
        get => _settingsService?.Get(SettingKeys.WorkstationWorkEndTime, "18:00") ?? "18:00";
        set => _ = SetSettingAsync(SettingKeys.WorkstationWorkEndTime, value);
    }

    public string WorkstationLunchStartTime
    {
        get => _settingsService?.Get(SettingKeys.WorkstationLunchStartTime, "12:00") ?? "12:00";
        set => _ = SetSettingAsync(SettingKeys.WorkstationLunchStartTime, value);
    }

    public string WorkstationLunchEndTime
    {
        get => _settingsService?.Get(SettingKeys.WorkstationLunchEndTime, "13:00") ?? "13:00";
        set => _ = SetSettingAsync(SettingKeys.WorkstationLunchEndTime, value);
    }

    public string WorkstationWorkdays
    {
        get => _settingsService?.Get(SettingKeys.WorkstationWorkdays, "1,2,3,4,5") ?? "1,2,3,4,5";
        set => _ = SetSettingAsync(SettingKeys.WorkstationWorkdays, value);
    }

    public int WorkstationPayday
    {
        get => _settingsService?.Get(SettingKeys.WorkstationPayday, 15) ?? 15;
        set => _ = SetSettingAsync(SettingKeys.WorkstationPayday, Math.Clamp(value, 1, 28));
    }

    public string WorkBearTextStyle
    {
        get => _settingsService?.Get(SettingKeys.WorkBearTextStyle, "打工人模式") ?? "打工人模式";
        set => _ = SetSettingAsync(SettingKeys.WorkBearTextStyle, value);
    }

    public string[] WorkBearTextStyleOptions { get; } = ["正常模式", "打工人模式", "抽象模式"];

    public IReadOnlyList<WorkTemplateOption> WorkTemplateOptions { get; } =
    [
        new("标准双休 09:00-18:00", "09:00", "18:00", "12:00", "13:00", "1,2,3,4,5"),
        new("单休 09:00-18:30", "09:00", "18:30", "12:00", "13:30", "1,2,3,4,5,6"),
        new("大小周 09:00-18:30", "09:00", "18:30", "12:00", "13:30", "1,2,3,4,5"),
        new("996 09:00-21:00", "09:00", "21:00", "12:00", "13:00", "1,2,3,4,5,6"),
        new("自定义", "09:00", "18:00", "12:00", "13:00", "1,2,3,4,5")
    ];

    private bool ShowFishingValue => _settingsService?.Get(SettingKeys.WorkstationShowFishingValue, true) ?? true;
    private bool ShowOffWorkCountdown => _settingsService?.Get(SettingKeys.WorkstationShowOffWorkCountdown, true) ?? true;

    public async Task RefreshAsync()
    {
        _snapshot = await _dashboardService.GetSnapshotAsync(DateTimeOffset.Now, CancellationToken.None);
        RaiseSnapshotProperties();
    }

    public async Task StartFishingAsync()
    {
        await _dashboardService.StartFishingAsync(DateTimeOffset.Now, CancellationToken.None);
        LastMessage = "小熊已进入静默观察模式。";
        await RefreshAsync();
    }

    public async Task EndFishingAsync()
    {
        await _dashboardService.EndFishingAsync(DateTimeOffset.Now, CancellationToken.None);
        LastMessage = "本次摸鱼已计入今日报告。";
        await RefreshAsync();
    }

    public Task ToggleFishingAsync() => _snapshot.IsFishing ? EndFishingAsync() : StartFishingAsync();

    public async Task ClearFishingAsync()
    {
        await _dashboardService.ClearTodayFishingAsync(DateOnly.FromDateTime(DateTime.Today), CancellationToken.None);
        LastMessage = "今日摸鱼记录已清除。";
        await RefreshAsync();
    }

    public async Task ResetTodayAsync()
    {
        await _dashboardService.ResetTodayAsync(DateOnly.FromDateTime(DateTime.Today), CancellationToken.None);
        LastMessage = "今日统计已重置。";
        await RefreshAsync();
    }

    public async Task GenerateReportAsync()
    {
        var report = await _dashboardService.GenerateDailyReportAsync(DateTimeOffset.Now, CancellationToken.None);
        LastMessage = report.ReportText;
        await RefreshAsync();
    }

    public async Task GenerateShareCardAsync()
    {
        if (_shareCardService is null)
        {
            LastMessage = "分享卡片服务未加载。";
            return;
        }

        if (_settingsService?.Get(SettingKeys.EnableWorkBearShareCard, true) == false)
        {
            LastMessage = "分享卡片功能已关闭。";
            return;
        }

        var path = await _shareCardService.GenerateTodayCardAsync(CancellationToken.None);
        LastMessage = $"今日生存报告已生成，里面不包含剪贴板内容：{path}";
    }

    public async Task SetSprintAsync(bool enabled)
    {
        await _dashboardService.SetSprintModeAsync(enabled, CancellationToken.None);
        LastMessage = enabled ? "下班冲刺开始，建议进入低功耗模式。" : "下班冲刺已关闭。";
        await RefreshAsync();
    }

    public async Task ToggleRestReminderAsync()
    {
        if (_snapshot.RestReminderEnabled)
        {
            await MuteRestReminderTodayAsync();
            return;
        }

        if (_settingsService is null)
        {
            return;
        }

        await _settingsService.SetAsync(SettingKeys.WorkstationEnableOverworkReminder, true, CancellationToken.None);
        await _settingsService.SetAsync(SettingKeys.WorkBearRestReminderMutedDate, string.Empty, CancellationToken.None);
        await _settingsService.SetAsync(SettingKeys.WorkBearRestReminderSnoozedUntil, string.Empty, CancellationToken.None);
        LastMessage = "休息提醒已开启。";
        await RefreshAsync();
    }

    public async Task SnoozeRestReminderAsync()
    {
        if (_overworkReminderService is not null)
        {
            await _overworkReminderService.SnoozeAsync(DateTimeOffset.Now, CancellationToken.None);
        }
        else if (_settingsService is not null)
        {
            await _settingsService.SetAsync(SettingKeys.WorkBearRestReminderSnoozedUntil, DateTimeOffset.Now.AddMinutes(15).ToString("O"), CancellationToken.None);
        }

        LastMessage = "已稍后提醒，小熊先退下。";
        await RefreshAsync();
    }

    public async Task MuteRestReminderTodayAsync()
    {
        var now = DateTimeOffset.Now;
        if (_overworkReminderService is not null)
        {
            await _overworkReminderService.MuteTodayAsync(now, CancellationToken.None);
        }
        else if (_settingsService is not null)
        {
            await _settingsService.SetAsync(SettingKeys.WorkBearRestReminderMutedDate, DateOnly.FromDateTime(now.Date).ToString("yyyy-MM-dd"), CancellationToken.None);
        }

        LastMessage = "今天不再提醒。";
        await RefreshAsync();
    }

    public async Task DisableRestReminderAsync()
    {
        if (_settingsService is null)
        {
            return;
        }

        await _settingsService.SetAsync(SettingKeys.WorkstationEnableOverworkReminder, false, CancellationToken.None);
        LastMessage = "休息提醒已关闭。";
        await RefreshAsync();
    }

    public async Task MuteDailyReportTodayAsync()
    {
        if (_settingsService is null)
        {
            return;
        }

        await _settingsService.SetAsync(SettingKeys.WorkBearDailyReportMutedDate, DateOnly.FromDateTime(DateTime.Today).ToString("yyyy-MM-dd"), CancellationToken.None);
        LastMessage = "今日自动报告不再显示。";
        await RefreshAsync();
    }

    public async Task ToggleAutoDailyReportAsync()
    {
        if (_settingsService is null)
        {
            return;
        }

        var enabled = !AutoShowDailyReport;
        await _settingsService.SetAsync(SettingKeys.AutoShowDailyWorkReport, enabled, CancellationToken.None);
        LastMessage = enabled ? "下班后自动报告已开启。" : "下班后自动报告已关闭。";
        OnPropertyChanged(nameof(AutoShowDailyReport));
        OnPropertyChanged(nameof(AutoDailyReportButtonText));
    }

    private void ApplyWorkTemplate(object? parameter)
    {
        if (parameter is not WorkTemplateOption template)
        {
            return;
        }

        WorkstationWorkStartTime = template.WorkStartTime;
        WorkstationWorkEndTime = template.WorkEndTime;
        WorkstationLunchStartTime = template.LunchStartTime;
        WorkstationLunchEndTime = template.LunchEndTime;
        WorkstationWorkdays = template.Workdays;
        LastMessage = $"已应用工作制模板：{template.Name}";
    }

    private async Task SetSettingAsync<T>(string key, T value)
    {
        if (_settingsService is null)
        {
            return;
        }

        await _settingsService.SetAsync(key, value, CancellationToken.None);
        OnPropertyChanged(key);
        await RefreshAsync();
        ValidateWorkRuleMessage();
    }

    private void ValidateWorkRuleMessage()
    {
        if (TimeOnly.TryParse(WorkstationWorkStartTime, out var start) &&
            TimeOnly.TryParse(WorkstationWorkEndTime, out var end) &&
            end <= start)
        {
            LastMessage = "下班时间不能早于或等于上班时间，请调整工作规则。";
            return;
        }

        if (!TimeOnly.TryParse(WorkstationWorkStartTime, out _) ||
            !TimeOnly.TryParse(WorkstationWorkEndTime, out _))
        {
            LastMessage = "时间格式建议使用 HH:mm，例如 09:00。";
        }
    }

    private void RaiseSnapshotProperties()
    {
        foreach (var name in new[]
        {
            nameof(Title), nameof(Subtitle), nameof(CurrentStateText), nameof(WorkStatusText), nameof(BearStatusText), nameof(BearLineText),
            nameof(OffWorkCountdownText), nameof(TodayEarnedText), nameof(MonthEarnedText), nameof(MinuteValueText), nameof(BossPaidText), nameof(PaydayText),
            nameof(FishingButtonText), nameof(CurrentFishingText), nameof(CurrentFishingValueText), nameof(TodayFishingDurationText), nameof(TodayFishingValueText),
            nameof(CopyCountText), nameof(PasteCountText), nameof(GestureCountText), nameof(SavedClicksText), nameof(OpenClipboardCountText), nameof(ActionStatsText),
            nameof(ProtectionHintText), nameof(WorkTipText), nameof(ContinuousWorkText), nameof(NextRestReminderText), nameof(RestReminderCountText), nameof(RestRiskText),
            nameof(RestReminderButtonText), nameof(SprintCountdownText), nameof(SprintSuggestionText), nameof(DailyRatingText), nameof(DailyReportText), nameof(SalaryHintText),
            nameof(AutoShowDailyReport), nameof(AutoDailyReportButtonText)
        })
        {
            OnPropertyChanged(name);
        }
    }

    private static string FormatDuration(TimeSpan value)
    {
        if (value <= TimeSpan.Zero) return "0 分钟";
        if (value.TotalHours >= 1) return $"{(int)value.TotalHours}小时 {value.Minutes}分钟";
        return $"{Math.Max(0, value.Minutes)}分钟";
    }

    private static string FormatDurationClock(TimeSpan value)
    {
        if (value <= TimeSpan.Zero) return "到点了";
        return $"{(int)value.TotalHours:D2}:{value.Minutes:D2}:{value.Seconds:D2}";
    }

    private static string FormatMoney(decimal value) => $"￥{value:F2}";

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private static readonly WorkstationDashboardSnapshot EmptySnapshot = new(
        "工位小熊",
        "坐在你电脑里的打工人状态 Hub",
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
        "已下班");

    public sealed record WorkTemplateOption(
        string Name,
        string WorkStartTime,
        string WorkEndTime,
        string LunchStartTime,
        string LunchEndTime,
        string Workdays);
}
