using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using GestureClip.Core.Abstractions;
using GestureClip.Core.Settings;
using GestureClip.Core.Workstation;

namespace GestureClip.App.ViewModels;

using Media = System.Windows.Media;

public sealed partial class WorkstationDashboardViewModel : INotifyPropertyChanged
{
    private readonly IWorkstationDashboardService _dashboardService;
    private readonly TimeProvider _timeProvider;
    private readonly ISettingsService? _settingsService;
    private readonly IWorkBearShareCardService? _shareCardService;
    private readonly IOverworkReminderService? _overworkReminderService;
    private readonly IConfirmationService? _confirmationService;
    private readonly IWorkstationStatsRepository? _statsRepository;
    private WorkstationDashboardSnapshot _snapshot = EmptySnapshot;
    private string _lastMessage = "本地统计，不上传。小熊只陪你看看状态，不偷看剪贴板正文。";
    private bool _settingsExpanded;
    private bool _earningsExpanded = true;
    private bool _fishingExpanded;
    private bool _restExpanded;
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
        IConfirmationService? confirmationService = null,
        IWorkstationStatsRepository? statsRepository = null,
        TimeProvider? timeProvider = null)
    {
        _dashboardService = dashboardService;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _snapshotAt = _timeProvider.GetLocalNow();
        _settingsService = settingsService;
        _shareCardService = shareCardService;
        _overworkReminderService = overworkReminderService;
        _confirmationService = confirmationService;
        _statsRepository = statsRepository;
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
        InitializeSnapshotNotifications();
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

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        if (propertyName is not null && _snapshotNotifications.TryGetValue(propertyName, out var notify))
        {
            notify();
            return;
        }
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
