using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Media = System.Windows.Media;
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
    private readonly IConfirmationService? _confirmationService;
    private WorkstationDashboardSnapshot _snapshot = EmptySnapshot;
    private string _lastMessage = "本地统计，不上传。小熊只陪你看看状态，不偷看剪贴板正文。";
    private bool _settingsExpanded;
    private bool _earningsExpanded = true;
    private bool _fishingExpanded = true;
    private bool _restExpanded = true;
    private bool _sprintExpanded;
    private bool _reportExpanded;
    private string _setupSalaryText = "";
    private string _setupStartTime = "09:00";
    private string _setupEndTime = "18:00";
    private string _periodReportText = "点下面按钮生成本地周报或近 30 天总结，不上传。";
    private bool _periodExpanded;

    public WorkstationDashboardViewModel(
        IWorkstationDashboardService dashboardService,
        ISettingsService? settingsService = null,
        IWorkBearShareCardService? shareCardService = null,
        IOverworkReminderService? overworkReminderService = null,
        IConfirmationService? confirmationService = null)
    {
        _dashboardService = dashboardService;
        _settingsService = settingsService;
        _shareCardService = shareCardService;
        _overworkReminderService = overworkReminderService;
        _confirmationService = confirmationService;
        _setupSalaryText = WorkstationMonthlySalary > 0 ? WorkstationMonthlySalary.ToString("0") : "10000";
        _setupStartTime = WorkstationWorkStartTime;
        _setupEndTime = WorkstationWorkEndTime;
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
        CompleteSetupCommand = new AsyncRelayCommand(_ => CompleteSetupAsync());
        DismissSetupCommand = new AsyncRelayCommand(_ => DismissSetupAsync());
        OpenWorkRulesCommand = new RelayCommand(_ => OpenWorkRules());
        GenerateWeeklyReportCommand = new AsyncRelayCommand(_ => GeneratePeriodReportAsync(7));
        GenerateMonthlyReportCommand = new AsyncRelayCommand(_ => GeneratePeriodReportAsync(30));
        OpenLastShareCardFolderCommand = new AsyncRelayCommand(_ => OpenLastShareCardFolderAsync());
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
    public ICommand CompleteSetupCommand { get; }
    public ICommand DismissSetupCommand { get; }
    public ICommand OpenWorkRulesCommand { get; }
    public ICommand GenerateWeeklyReportCommand { get; }
    public ICommand GenerateMonthlyReportCommand { get; }
    public ICommand OpenLastShareCardFolderCommand { get; }

    private string? _lastShareCardPath;

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
    public string PaydayText => _snapshot.DaysUntilPayday <= 0 ? "就是今天" : $"还剩 {_snapshot.DaysUntilPayday} 天";
    public string FishingButtonText => _snapshot.IsFishing ? "结束本次摸鱼" : "开始摸鱼计时";
    public bool IsFishing => _snapshot.IsFishing;
    public string FishingStatusBanner => _snapshot.IsFishing
        ? $"摸鱼计时中 · 已进行 {FormatDuration(_snapshot.CurrentFishingDuration)}"
        : string.Empty;
    public Media.Brush FishingBorderBrush => IsFishing
        ? ParseBrush("#2DD4BF", "#2DD4BF")
        : ParseBrush("#00000000", "#00000000");
    public Thickness FishingBorderThickness => IsFishing ? new Thickness(1.5) : new Thickness(0);
    public double HeroBorderOpacity => IsFishing ? 1d : 0d;

    public bool HasDailyReport => !string.IsNullOrWhiteSpace(_snapshot.DailyReportText);
    public string DailyReportPlaceholder => "点「生成今日报告」看看今天的工位小结（仅本地，可分享为 PNG）。";
    public string DailyReportDisplayText => HasDailyReport ? _snapshot.DailyReportText : DailyReportPlaceholder;
    public string CurrentFishingText => FormatDuration(_snapshot.CurrentFishingDuration);
    public string CurrentFishingValueText => ShowFishingValue ? FormatMoney(_snapshot.CurrentFishingValue) : "已隐藏";
    public string TodayFishingDurationText => FormatDuration(_snapshot.TodayFishingDuration);
    public string TodayFishingValueText => ShowFishingValue ? FormatMoney(_snapshot.TodayFishingValue) : "已隐藏";
    public string CopyCountText => _snapshot.CopyCount.ToString();
    public string PasteCountText => _snapshot.PasteCount.ToString();
    public string GestureCountText => _snapshot.GestureCount.ToString();
    public string SavedClicksText => _snapshot.EstimatedSavedClicks <= 0 ? "今天还没省到点击" : $"大约省了 {_snapshot.EstimatedSavedClicks} 次点击";
    public string OpenClipboardCountText => _snapshot.OpenClipboardCount.ToString();
    public string ActionStatsText => $"复制 {_snapshot.CopyCount} · 粘贴 {_snapshot.PasteCount} · 手势 {_snapshot.GestureCount} · 打开剪贴板 {_snapshot.OpenClipboardCount}";
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
    public string StageAccentColor => string.IsNullOrWhiteSpace(_snapshot.StageAccentColor) ? "#60A5FA" : _snapshot.StageAccentColor;
    public string StageStartColor => string.IsNullOrWhiteSpace(_snapshot.StageStartColor) ? "#1D4ED8" : _snapshot.StageStartColor;
    public Media.Brush StageAccentBrush => ParseBrush(StageAccentColor, "#60A5FA");
    public Media.Brush StageStartBrush => ParseBrush(StageStartColor, "#1D4ED8");
    public Media.Brush StageSoftBrush
    {
        get
        {
            var color = ParseColor(StageAccentColor, "#60A5FA");
            color.A = 0x33;
            return new Media.SolidColorBrush(color);
        }
    }
    public Media.Brush RestRiskBrush => ParseBrush(RestRiskAccentColor, "#94A3B8");
    public Media.Brush RestRiskSoftBrush
    {
        get
        {
            var color = ParseColor(RestRiskAccentColor, "#94A3B8");
            color.A = 0x33;
            return new Media.SolidColorBrush(color);
        }
    }
    public string RestRiskAccentColor => _snapshot.RestRiskLevel switch
    {
        "critical" => "#F87171",
        "high" => "#FB923C",
        "caution" => "#FBBF24",
        _ => "#94A3B8"
    };
    public string RestRiskLevel => string.IsNullOrWhiteSpace(_snapshot.RestRiskLevel) ? "normal" : _snapshot.RestRiskLevel;
    public double WorkdayProgress => Math.Clamp(_snapshot.WorkdayProgress, 0d, 1d);
    public double WorkdayProgressPercent => Math.Round(WorkdayProgress * 100d, 0);
    public string WorkdayProgressText
    {
        get
        {
            if (_snapshot.WorkTimeStage == WorkTimeStage.RestDay)
            {
                return "今天是休息日";
            }

            if (_snapshot.WorkTimeStage == WorkTimeStage.BeforeWork)
            {
                return "还没到上班点";
            }

            if (_snapshot.WorkTimeStage == WorkTimeStage.Overtime)
            {
                return "已过下班线 · 注意收工";
            }

            if (_snapshot.WorkTimeStage == WorkTimeStage.OffWork)
            {
                return "今日工位已挂起";
            }

            return $"今日进度 {WorkdayProgressPercent:0}%";
        }
    }
    public double OffWorkProgress
    {
        get
        {
            // Mirror workday progress for countdown bar: 0 just started, 1 near/after off-work.
            if (_snapshot.WorkTimeStage is WorkTimeStage.Overtime or WorkTimeStage.OffWork)
            {
                return 1d;
            }

            if (_snapshot.WorkTimeStage is WorkTimeStage.BeforeWork or WorkTimeStage.RestDay)
            {
                return 0d;
            }

            return WorkdayProgress;
        }
    }
    public string OffWorkProgressText
    {
        get
        {
            if (!ShowOffWorkCountdown)
            {
                return "倒计时已隐藏";
            }

            if (_snapshot.TimeUntilOffWork <= TimeSpan.Zero)
            {
                return _snapshot.WorkTimeStage == WorkTimeStage.Overtime ? "已过下班点" : "到点了";
            }

            return $"距离下班 {FormatDurationClock(_snapshot.TimeUntilOffWork)}";
        }
    }

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

    public bool NeedsSetup
    {
        get
        {
            if (_settingsService is null)
            {
                return false;
            }

            if (_settingsService.Get(SettingKeys.WorkBearSetupCompleted, false))
            {
                return false;
            }

            return WorkstationMonthlySalary <= 0m;
        }
    }

    /// <summary>
    /// Setup dismissed or completed path, but salary still missing — soft guide instead of cold ¥0.
    /// </summary>
    public bool ShowEmptySalaryGuide =>
        _settingsService is not null
        && !NeedsSetup
        && WorkstationMonthlySalary <= 0m;

    public string EmptySalaryGuideTitle => "收益还是 ¥0.00？";

    public string EmptySalaryGuideText =>
        "还没填月薪时，今日已赚只能显示 0。补一下月薪和上下班时间，小熊就能本地估算（不上传）。";

    public string EmptySalaryGuideActionText => "去填写月薪与工时";


    public string SetupSalaryText
    {
        get => _setupSalaryText;
        set
        {
            if (_setupSalaryText == value) return;
            _setupSalaryText = value;
            OnPropertyChanged();
        }
    }

    public string SetupStartTime
    {
        get => _setupStartTime;
        set
        {
            if (_setupStartTime == value) return;
            _setupStartTime = value;
            OnPropertyChanged();
        }
    }

    public string SetupEndTime
    {
        get => _setupEndTime;
        set
        {
            if (_setupEndTime == value) return;
            _setupEndTime = value;
            OnPropertyChanged();
        }
    }

    public bool EarningsExpanded
    {
        get => _earningsExpanded;
        set { if (_earningsExpanded == value) return; _earningsExpanded = value; OnPropertyChanged(); }
    }

    public bool FishingExpanded
    {
        get => _fishingExpanded;
        set { if (_fishingExpanded == value) return; _fishingExpanded = value; OnPropertyChanged(); }
    }

    public bool RestExpanded
    {
        get => _restExpanded;
        set { if (_restExpanded == value) return; _restExpanded = value; OnPropertyChanged(); }
    }

    public bool SprintExpanded
    {
        get => _sprintExpanded;
        set { if (_sprintExpanded == value) return; _sprintExpanded = value; OnPropertyChanged(); }
    }

    public bool ReportExpanded
    {
        get => _reportExpanded;
        set { if (_reportExpanded == value) return; _reportExpanded = value; OnPropertyChanged(); }
    }

    public bool PeriodExpanded
    {
        get => _periodExpanded;
        set { if (_periodExpanded == value) return; _periodExpanded = value; OnPropertyChanged(); }
    }

    public int RestReminderMaxPerDay
    {
        get => Math.Clamp(_settingsService?.Get(SettingKeys.WorkstationOverworkReminderMaxPerDay, 4) ?? 4, 1, 12);
        set => _ = SetSettingAsync(SettingKeys.WorkstationOverworkReminderMaxPerDay, Math.Clamp(value, 1, 12));
    }

    public int RestReminderMaxPerWeek
    {
        get => Math.Clamp(_settingsService?.Get(SettingKeys.WorkstationOverworkReminderMaxPerWeek, 16) ?? 16, 1, 40);
        set => _ = SetSettingAsync(SettingKeys.WorkstationOverworkReminderMaxPerWeek, Math.Clamp(value, 1, 40));
    }

    public int RestReminderMinContinuousMinutes
    {
        get => Math.Clamp(_settingsService?.Get(SettingKeys.WorkstationOverworkReminderMinContinuousMinutes, 45) ?? 45, 15, 180);
        set => _ = SetSettingAsync(SettingKeys.WorkstationOverworkReminderMinContinuousMinutes, Math.Clamp(value, 15, 180));
    }

    public IReadOnlyList<int> RestReminderMaxOptions { get; } = [1, 2, 3, 4, 5, 6, 8, 10, 12];
    public IReadOnlyList<int> RestReminderWeekMaxOptions { get; } = [4, 8, 12, 16, 20, 28, 40];
    public IReadOnlyList<int> RestReminderMinContinuousOptions { get; } = [15, 30, 45, 60, 90, 120];

    public string RestReminderLimitText
    {
        get
        {
            var weekKey = _settingsService?.Get(SettingKeys.WorkBearRestReminderWeekKey, "") ?? "";
            var weekCount = _settingsService?.Get(SettingKeys.WorkBearRestReminderWeekCount, 0) ?? 0;
            return $"今日 {_snapshot.RestReminderCount}/{RestReminderMaxPerDay} · 本周 {weekCount}/{RestReminderMaxPerWeek} · 连续≥{RestReminderMinContinuousMinutes} 分钟才弹常规定时提醒";
        }
    }

    public string PrivacyHintText => "隐私：不记网页、不记剪贴板正文、不上传；摸鱼只记你手动开始/结束。";

    public string GreetingText
    {
        get
        {
            var hour = DateTimeOffset.Now.Hour;
            if (_snapshot.IsFishing)
            {
                return "摸鱼中 · 别慌";
            }

            if (_snapshot.WorkTimeStage == WorkTimeStage.Overtime)
            {
                return "加班区 · 记得存档";
            }

            if (_snapshot.WorkTimeStage == WorkTimeStage.LunchBreak)
            {
                return "午休 · 先充电";
            }

            if (_snapshot.WorkTimeStage == WorkTimeStage.RestDay)
            {
                return "休息日 · 慢一点";
            }

            if (_snapshot.WorkTimeStage is WorkTimeStage.OffWork or WorkTimeStage.BeforeWork)
            {
                return hour < 12 ? "早上好 · 未开工" : "已下班 · 低功耗";
            }

            return hour switch
            {
                < 11 => "上午好 · 稳住开局",
                < 14 => "中午好 · 节奏别崩",
                < 17 => "下午好 · 继续苟住",
                < 19 => "傍晚 · 准备收尾",
                _ => "晚上好 · 别硬撑"
            };
        }
    }


    public string PeriodReportText
    {
        get => _periodReportText;
        private set
        {
            if (_periodReportText == value) return;
            _periodReportText = value;
            OnPropertyChanged();
        }
    }

    public string ShareCardStyle
    {
        get => _settingsService?.Get(SettingKeys.WorkBearReportCardStyle, "经典蓝") ?? "经典蓝";
        set => _ = SetSettingAsync(SettingKeys.WorkBearReportCardStyle, value);
    }

    public string[] ShareCardStyleOptions { get; } = ["经典蓝", "简洁白", "吐槽风", "数据风"];

    public bool HudFunEnabled
    {
        get => _settingsService?.Get(SettingKeys.WorkBearHudFunEnabled, true) ?? true;
        set => _ = SetSettingAsync(SettingKeys.WorkBearHudFunEnabled, value);
    }

    public bool GestureXpBonusEnabled
    {
        get => _settingsService?.Get(SettingKeys.WorkBearGestureXpBonusEnabled, true) ?? true;
        set => _ = SetSettingAsync(SettingKeys.WorkBearGestureXpBonusEnabled, value);
    }

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
        LastMessage = "摸鱼计时开始了，记得回来点结束。";
        await RefreshAsync();
    }

    public async Task EndFishingAsync()
    {
        await _dashboardService.EndFishingAsync(DateTimeOffset.Now, CancellationToken.None);
        LastMessage = "本次摸鱼已记下，不会上传。";
        await RefreshAsync();
    }

    public Task ToggleFishingAsync() => _snapshot.IsFishing ? EndFishingAsync() : StartFishingAsync();

    public async Task ClearFishingAsync()
    {
        await _dashboardService.ClearTodayFishingAsync(DateOnly.FromDateTime(DateTime.Today), CancellationToken.None);
        LastMessage = "今日摸鱼记录已清空。";
        await RefreshAsync();
    }

    public async Task ResetTodayAsync()
    {
        if (_confirmationService is not null)
        {
            var confirmed = _confirmationService.Confirm(
                "重置今日统计",
                "将清空今天的摸鱼计时、复制/粘贴/手势次数等本地统计。\n此操作不可撤销，确定继续？");
            if (!confirmed)
            {
                LastMessage = "已取消重置。";
                return;
            }
        }

        await _dashboardService.ResetTodayAsync(DateOnly.FromDateTime(DateTime.Today), CancellationToken.None);
        LastMessage = "今日统计已重置，可以从零开始。";
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

        var path = await _shareCardService.GenerateTodayCardAsync(ShareCardStyle, CancellationToken.None);
        _lastShareCardPath = path;
        LastMessage = $"分享卡片已生成（{ShareCardStyle}，不含剪贴板内容）：{path}";
        _shareCardService.OpenCardFolder(path);
    }

    public async Task GeneratePeriodReportAsync(int dayCount)
    {
        PeriodReportText = await _dashboardService.GeneratePeriodReportAsync(DateTimeOffset.Now, dayCount, CancellationToken.None);
        PeriodExpanded = true;
        LastMessage = dayCount <= 7 ? "本周总结已生成（仅本地）。" : "近 30 天总结已生成（仅本地）。";
    }

    public Task OpenLastShareCardFolderAsync()
    {
        if (_shareCardService is null || string.IsNullOrWhiteSpace(_lastShareCardPath))
        {
            LastMessage = "还没有生成过分享卡片。";
            return Task.CompletedTask;
        }

        _shareCardService.OpenCardFolder(_lastShareCardPath);
        LastMessage = "已打开卡片所在文件夹。";
        return Task.CompletedTask;
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

        LastMessage = "好，稍后提醒。小熊先退下。";
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

        LastMessage = "今天不再打扰你。";
        await RefreshAsync();
    }

    public async Task DisableRestReminderAsync()
    {
        if (_settingsService is null)
        {
            return;
        }

        await _settingsService.SetAsync(SettingKeys.WorkstationEnableOverworkReminder, false, CancellationToken.None);
        LastMessage = "休息提醒已关闭，可随时再开。";
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
        SetupStartTime = template.WorkStartTime;
        SetupEndTime = template.WorkEndTime;
        LastMessage = $"已应用工作制模板：{template.Name}";
    }


    public void OpenWorkRules()
    {
        SettingsExpanded = true;
        LastMessage = "在下方补月薪和上下班时间即可，数据只保存在本机。";
        OnPropertyChanged(nameof(ShowEmptySalaryGuide));
    }

    public async Task CompleteSetupAsync()
    {
        if (_settingsService is null)
        {
            return;
        }

        if (!decimal.TryParse(SetupSalaryText.Trim(), out var salary) || salary <= 0)
        {
            LastMessage = "请先填写有效月薪，例如 10000。";
            return;
        }

        if (!TimeOnly.TryParse(SetupStartTime, out var start) ||
            !TimeOnly.TryParse(SetupEndTime, out var end) ||
            end <= start)
        {
            LastMessage = "请填写有效上下班时间（HH:mm），且下班晚于上班。";
            return;
        }

        await _settingsService.SetAsync(SettingKeys.WorkstationMonthlySalary, salary, CancellationToken.None);
        await _settingsService.SetAsync(SettingKeys.WorkstationWorkStartTime, SetupStartTime.Trim(), CancellationToken.None);
        await _settingsService.SetAsync(SettingKeys.WorkstationWorkEndTime, SetupEndTime.Trim(), CancellationToken.None);
        await _settingsService.SetAsync(SettingKeys.WorkBearSetupCompleted, true, CancellationToken.None);
        OnPropertyChanged(nameof(WorkstationMonthlySalary));
        OnPropertyChanged(nameof(WorkstationWorkStartTime));
        OnPropertyChanged(nameof(WorkstationWorkEndTime));
        OnPropertyChanged(nameof(NeedsSetup));
        OnPropertyChanged(nameof(ShowEmptySalaryGuide));
        LastMessage = "30 秒配置完成。小熊已按你的月薪和工时估算今日状态。";
        await RefreshAsync();
    }

    public async Task DismissSetupAsync()
    {
        if (_settingsService is null)
        {
            return;
        }

        await _settingsService.SetAsync(SettingKeys.WorkBearSetupCompleted, true, CancellationToken.None);
        OnPropertyChanged(nameof(NeedsSetup));
        OnPropertyChanged(nameof(ShowEmptySalaryGuide));
        LastMessage = "已跳过引导。可随时在下方「工作规则设置」里补月薪和上下班时间，或点上方引导卡片。";
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
            nameof(FishingButtonText), nameof(IsFishing), nameof(FishingStatusBanner), nameof(FishingBorderBrush), nameof(FishingBorderThickness), nameof(HeroBorderOpacity), nameof(HasDailyReport), nameof(DailyReportDisplayText), nameof(CurrentFishingText), nameof(CurrentFishingValueText), nameof(TodayFishingDurationText), nameof(TodayFishingValueText),
            nameof(CopyCountText), nameof(PasteCountText), nameof(GestureCountText), nameof(SavedClicksText), nameof(OpenClipboardCountText), nameof(ActionStatsText),
            nameof(ProtectionHintText), nameof(WorkTipText), nameof(ContinuousWorkText), nameof(NextRestReminderText), nameof(RestReminderCountText), nameof(RestRiskText),
            nameof(RestReminderButtonText), nameof(SprintCountdownText), nameof(SprintSuggestionText), nameof(DailyRatingText), nameof(DailyReportText), nameof(SalaryHintText),
            nameof(AutoShowDailyReport), nameof(AutoDailyReportButtonText), nameof(NeedsSetup), nameof(ShowEmptySalaryGuide), nameof(RestReminderLimitText), nameof(RestReminderMaxPerDay),
            nameof(RestReminderMaxPerWeek), nameof(RestReminderMinContinuousMinutes), nameof(PrivacyHintText), nameof(GreetingText), nameof(PeriodReportText),
            nameof(StageAccentColor), nameof(StageStartColor), nameof(StageAccentBrush), nameof(StageStartBrush), nameof(StageSoftBrush), nameof(RestRiskBrush), nameof(RestRiskSoftBrush), nameof(RestRiskAccentColor), nameof(RestRiskLevel), nameof(WorkdayProgress), nameof(WorkdayProgressPercent), nameof(WorkdayProgressText), nameof(OffWorkProgress), nameof(OffWorkProgressText), nameof(ShareCardStyle), nameof(HudFunEnabled), nameof(GestureXpBonusEnabled)
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


    private static Media.Brush ParseBrush(string? hex, string fallback)
    {
        return new Media.SolidColorBrush(ParseColor(hex, fallback));
    }

    private static Media.Color ParseColor(string? hex, string fallback)
    {
        var value = string.IsNullOrWhiteSpace(hex) ? fallback : hex.Trim();
        try
        {
            return (Media.Color)Media.ColorConverter.ConvertFromString(value)!;
        }
        catch
        {
            return (Media.Color)Media.ColorConverter.ConvertFromString(fallback)!;
        }
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
