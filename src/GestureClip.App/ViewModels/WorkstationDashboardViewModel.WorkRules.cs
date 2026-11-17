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
    public string GreetingText
    {
        get
        {
            var hour = _timeProvider.GetLocalNow().Hour;
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

    public bool WaterReminderEnabled
    {
        get => _settingsService?.Get(SettingKeys.WellnessWaterReminderEnabled, false) ?? false;
        set => _ = SetSettingAsync(SettingKeys.WellnessWaterReminderEnabled, value);
    }

    public int WaterReminderIntervalMinutes
    {
        get => _settingsService?.Get(SettingKeys.WellnessWaterIntervalMinutes, 60) ?? 60;
        set => _ = SetSettingAsync(SettingKeys.WellnessWaterIntervalMinutes, value);
    }

    public bool StretchReminderEnabled
    {
        get => _settingsService?.Get(SettingKeys.WellnessStretchReminderEnabled, false) ?? false;
        set => _ = SetSettingAsync(SettingKeys.WellnessStretchReminderEnabled, value);
    }

    public int StretchReminderIntervalMinutes
    {
        get => _settingsService?.Get(SettingKeys.WellnessStretchIntervalMinutes, 90) ?? 90;
        set => _ = SetSettingAsync(SettingKeys.WellnessStretchIntervalMinutes, value);
    }

    public IReadOnlyList<int> WaterIntervalOptions { get; } = [30, 45, 60, 90, 120];

    public IReadOnlyList<int> StretchIntervalOptions { get; } = [45, 60, 90, 120, 180];

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
}
