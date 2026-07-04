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
    private WorkstationDashboardSnapshot _snapshot = EmptySnapshot;

    public WorkstationDashboardViewModel(IWorkstationDashboardService dashboardService, ISettingsService? settingsService = null)
    {
        _dashboardService = dashboardService;
        _settingsService = settingsService;
        RefreshCommand = new AsyncRelayCommand(_ => RefreshAsync());
        StartFishingCommand = new AsyncRelayCommand(_ => StartFishingAsync());
        EndFishingCommand = new AsyncRelayCommand(_ => EndFishingAsync());
        ResetTodayCommand = new AsyncRelayCommand(_ => ResetTodayAsync());
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ICommand RefreshCommand { get; }

    public ICommand StartFishingCommand { get; }

    public ICommand EndFishingCommand { get; }

    public ICommand ResetTodayCommand { get; }

    public string Title => _snapshot.Title;

    public string Subtitle => _snapshot.Subtitle;

    public string OffWorkCountdownText => ShowOffWorkCountdown ? FormatDuration(_snapshot.TimeUntilOffWork) : "已隐藏";

    public string TodayEarnedText => FormatMoney(_snapshot.TodayEarned);

    public string MonthEarnedText => FormatMoney(_snapshot.MonthEarned);

    public string PaydayText => $"{_snapshot.DaysUntilPayday} 天";

    public string FishingButtonText => _snapshot.IsFishing ? "正在摸鱼" : "开始摸鱼";

    public string CurrentFishingText => FormatDuration(_snapshot.CurrentFishingDuration);

    public string CurrentFishingValueText => ShowFishingValue ? FormatMoney(_snapshot.CurrentFishingValue) : "已隐藏";

    public string TodayFishingValueText => ShowFishingValue ? FormatMoney(_snapshot.TodayFishingValue) : "已隐藏";

    public string ActionStatsText => $"复制 {_snapshot.CopyCount} · 粘贴 {_snapshot.PasteCount} · 手势 {_snapshot.GestureCount}";

    public string SavedClicksText => $"少点了 {_snapshot.EstimatedSavedClicks} 次";

    public string WorkStatusText => _snapshot.WorkStatusText;

    public string ProtectionHintText => _snapshot.TimeUntilOffWork > TimeSpan.Zero &&
                                        _snapshot.TimeUntilOffWork <= TimeSpan.FromMinutes(30)
        ? "已进入下班保护期。建议谨慎接收新需求。"
        : "当前未进入下班保护期。";

    public string WorkTipText => _snapshot.WorkStatusText switch
    {
        "开机缓冲期" => "小熊提示：先启动人类系统，再启动工作系统。",
        "假装高效期" => "当前建议：复制粘贴，避免无效内耗。",
        "灵魂离线期" => "小熊提示：午间灵魂离线是正常现象。",
        "低功耗运行期" => "小熊提示：你不是效率低，你是在节能。",
        "禁止新增需求期" => "小熊提示：保护下班，人人有责。",
        _ => "小熊提示：非法占用人生时间，建议尽快撤离。"
    };

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
        await RefreshAsync();
    }

    public async Task EndFishingAsync()
    {
        await _dashboardService.EndFishingAsync(DateTimeOffset.Now, CancellationToken.None);
        await RefreshAsync();
    }

    public async Task ResetTodayAsync()
    {
        await _dashboardService.ResetTodayAsync(DateOnly.FromDateTime(DateTime.Today), CancellationToken.None);
        await RefreshAsync();
    }

    private void RaiseSnapshotProperties()
    {
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(Subtitle));
        OnPropertyChanged(nameof(OffWorkCountdownText));
        OnPropertyChanged(nameof(TodayEarnedText));
        OnPropertyChanged(nameof(MonthEarnedText));
        OnPropertyChanged(nameof(PaydayText));
        OnPropertyChanged(nameof(FishingButtonText));
        OnPropertyChanged(nameof(CurrentFishingText));
        OnPropertyChanged(nameof(CurrentFishingValueText));
        OnPropertyChanged(nameof(TodayFishingValueText));
        OnPropertyChanged(nameof(ActionStatsText));
        OnPropertyChanged(nameof(SavedClicksText));
        OnPropertyChanged(nameof(WorkStatusText));
        OnPropertyChanged(nameof(ProtectionHintText));
        OnPropertyChanged(nameof(WorkTipText));
    }

    private static string FormatDuration(TimeSpan value)
    {
        if (value <= TimeSpan.Zero)
        {
            return "到点了";
        }

        if (value.TotalHours >= 1)
        {
            return $"{(int)value.TotalHours}小时 {value.Minutes}分钟";
        }

        return $"{Math.Max(0, value.Minutes)}分钟";
    }

    private static string FormatMoney(decimal value) => $"￥{value:F2}";

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private static readonly WorkstationDashboardSnapshot EmptySnapshot = new(
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
        "低功耗运行期");
}
