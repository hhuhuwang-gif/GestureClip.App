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
            var weekCount = _settingsService?.Get(SettingKeys.WorkBearRestReminderWeekCount, 0) ?? 0;
            return $"今日 {_snapshot.RestReminderCount}/{RestReminderMaxPerDay} · 本周 {weekCount}/{RestReminderMaxPerWeek} · 连续≥{RestReminderMinContinuousMinutes} 分钟才弹常规定时提醒";
        }
    }

    public string PrivacyHintText => "隐私：不记网页、不记剪贴板正文、不上传；摸鱼只记你手动开始/结束。";
}
