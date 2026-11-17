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
    public string Title => _snapshot.Title;
    public string Subtitle => _snapshot.Subtitle;
    public string CurrentStateText => _snapshot.WorkStageText;
    public string WorkStatusText => _snapshot.WorkStatusText;
    public string BearStatusText => _snapshot.BearStatusText;
    public string BearLineText => _snapshot.BearLineText;
    public string OffWorkCountdownText => ShowOffWorkCountdown ? FormatDurationClock(ExtrapolatedTimeUntilOffWork) : "已隐藏";
    public string TodayEarnedText => FormatMoney(ExtrapolatedTodayEarned);
    public string MonthEarnedText => FormatMoney(_snapshot.MonthEarned);
    public string MinuteValueText => FormatMoney(_snapshot.MinuteValue);
    public string BossPaidText => FormatMoney(ExtrapolatedTodayEarned);
    public string PaydayText => _snapshot.DaysUntilPayday <= 0 ? "就是今天" : $"还剩 {_snapshot.DaysUntilPayday} 天";
    public bool HasOvertime => _snapshot.OvertimeDuration > TimeSpan.Zero;
    public string OvertimeText => HasOvertime
        ? $"已加班 {FormatDuration(_snapshot.OvertimeDuration)} · 约白送老板 {FormatMoney((decimal)_snapshot.OvertimeDuration.TotalMinutes * _snapshot.MinuteValue)}"
        : string.Empty;
    public string FishingButtonText => _snapshot.IsFishing ? "结束本次摸鱼" : "开始摸鱼计时";
    public bool IsFishing => _snapshot.IsFishing;
    public string FishingStatusBanner => _snapshot.IsFishing
        ? $"摸鱼计时中 · 已进行 {FormatDuration(_snapshot.CurrentFishingDuration)}"
        : string.Empty;
    public Media.Brush FishingBorderBrush => IsFishing
        ? _fishingBrush.Get("#2DD4BF")
        : _fishingBrush.Get("#00000000");
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
    public Media.Brush StageAccentBrush => _stageAccentBrush.Get(StageAccentColor);
    public Media.Brush StageStartBrush => _stageStartBrush.Get(StageStartColor);
    public Media.Brush StageSoftBrush => _stageSoftBrush.Get(StageAccentColor, SoftBrushAlpha);
    public Media.Brush RestRiskBrush => _restRiskBrush.Get(RestRiskAccentColor);
    public Media.Brush RestRiskSoftBrush => _restRiskSoftBrush.Get(RestRiskAccentColor, SoftBrushAlpha);
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

            return $"距离下班 {FormatDurationClock(ExtrapolatedTimeUntilOffWork)}";
        }
    }
}
