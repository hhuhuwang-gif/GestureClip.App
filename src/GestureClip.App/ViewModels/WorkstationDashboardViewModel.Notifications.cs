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
    private readonly Dictionary<string, Action> _snapshotNotifications = new(StringComparer.Ordinal);

    private void InitializeSnapshotNotifications()
    {
        Track(nameof(Title), () => Title);
        Track(nameof(Subtitle), () => Subtitle);
        Track(nameof(CurrentStateText), () => CurrentStateText);
        Track(nameof(WorkStatusText), () => WorkStatusText);
        Track(nameof(BearStatusText), () => BearStatusText);
        Track(nameof(BearLineText), () => BearLineText);
        Track(nameof(OffWorkCountdownText), () => OffWorkCountdownText);
        Track(nameof(TodayEarnedText), () => TodayEarnedText);
        Track(nameof(MonthEarnedText), () => MonthEarnedText);
        Track(nameof(MinuteValueText), () => MinuteValueText);
        Track(nameof(BossPaidText), () => BossPaidText);
        Track(nameof(PaydayText), () => PaydayText);
        Track(nameof(FishingButtonText), () => FishingButtonText);
        Track(nameof(IsFishing), () => IsFishing);
        Track(nameof(FishingStatusBanner), () => FishingStatusBanner);
        Track(nameof(FishingBorderBrush), () => FishingBorderBrush);
        Track(nameof(FishingBorderThickness), () => FishingBorderThickness);
        Track(nameof(HeroBorderOpacity), () => HeroBorderOpacity);
        Track(nameof(HasDailyReport), () => HasDailyReport);
        Track(nameof(DailyReportDisplayText), () => DailyReportDisplayText);
        Track(nameof(CurrentFishingText), () => CurrentFishingText);
        Track(nameof(CurrentFishingValueText), () => CurrentFishingValueText);
        Track(nameof(TodayFishingDurationText), () => TodayFishingDurationText);
        Track(nameof(TodayFishingValueText), () => TodayFishingValueText);
        Track(nameof(CopyCountText), () => CopyCountText);
        Track(nameof(PasteCountText), () => PasteCountText);
        Track(nameof(GestureCountText), () => GestureCountText);
        Track(nameof(SavedClicksText), () => SavedClicksText);
        Track(nameof(OpenClipboardCountText), () => OpenClipboardCountText);
        Track(nameof(ActionStatsText), () => ActionStatsText);
        Track(nameof(ProtectionHintText), () => ProtectionHintText);
        Track(nameof(WorkTipText), () => WorkTipText);
        Track(nameof(ContinuousWorkText), () => ContinuousWorkText);
        Track(nameof(NextRestReminderText), () => NextRestReminderText);
        Track(nameof(RestReminderCountText), () => RestReminderCountText);
        Track(nameof(RestRiskText), () => RestRiskText);
        InitializeDetailNotifications();
    }

    private void InitializeDetailNotifications()
    {
        Track(nameof(RestReminderButtonText), () => RestReminderButtonText);
        Track(nameof(SprintCountdownText), () => SprintCountdownText);
        Track(nameof(SprintSuggestionText), () => SprintSuggestionText);
        Track(nameof(DailyRatingText), () => DailyRatingText);
        Track(nameof(DailyReportText), () => DailyReportText);
        Track(nameof(SalaryHintText), () => SalaryHintText);
        Track(nameof(AutoShowDailyReport), () => AutoShowDailyReport);
        Track(nameof(AutoDailyReportButtonText), () => AutoDailyReportButtonText);
        Track(nameof(NeedsSetup), () => NeedsSetup);
        Track(nameof(ShowEmptySalaryGuide), () => ShowEmptySalaryGuide);
        Track(nameof(RestReminderLimitText), () => RestReminderLimitText);
        Track(nameof(RestReminderMaxPerDay), () => RestReminderMaxPerDay);
        Track(nameof(RestReminderMaxPerWeek), () => RestReminderMaxPerWeek);
        Track(nameof(RestReminderMinContinuousMinutes), () => RestReminderMinContinuousMinutes);
        Track(nameof(PrivacyHintText), () => PrivacyHintText);
        Track(nameof(GreetingText), () => GreetingText);
        Track(nameof(PeriodReportText), () => PeriodReportText);
        Track(nameof(StageAccentColor), () => StageAccentColor);
        Track(nameof(StageStartColor), () => StageStartColor);
        Track(nameof(StageAccentBrush), () => StageAccentBrush);
        Track(nameof(StageStartBrush), () => StageStartBrush);
        Track(nameof(StageSoftBrush), () => StageSoftBrush);
        Track(nameof(RestRiskBrush), () => RestRiskBrush);
        Track(nameof(RestRiskSoftBrush), () => RestRiskSoftBrush);
        Track(nameof(RestRiskAccentColor), () => RestRiskAccentColor);
        Track(nameof(RestRiskLevel), () => RestRiskLevel);
        Track(nameof(WorkdayProgress), () => WorkdayProgress);
        Track(nameof(WorkdayProgressPercent), () => WorkdayProgressPercent);
        Track(nameof(WorkdayProgressText), () => WorkdayProgressText);
        Track(nameof(OffWorkProgress), () => OffWorkProgress);
        Track(nameof(OffWorkProgressText), () => OffWorkProgressText);
        Track(nameof(ShareCardStyle), () => ShareCardStyle);
        Track(nameof(HudFunEnabled), () => HudFunEnabled);
        Track(nameof(GestureXpBonusEnabled), () => GestureXpBonusEnabled);
        Track(nameof(HolidayCountdownTexts), () => HolidayCountdownTexts);
        Track(nameof(HasOvertime), () => HasOvertime);
        Track(nameof(OvertimeText), () => OvertimeText);
        InitializeWorkRuleNotifications();
    }

    private void InitializeWorkRuleNotifications()
    {
        Track(nameof(WorkstationMonthlySalary), () => WorkstationMonthlySalary);
        Track(nameof(WorkstationWorkStartTime), () => WorkstationWorkStartTime);
        Track(nameof(WorkstationWorkEndTime), () => WorkstationWorkEndTime);
        Track(nameof(WorkstationLunchStartTime), () => WorkstationLunchStartTime);
        Track(nameof(WorkstationLunchEndTime), () => WorkstationLunchEndTime);
        Track(nameof(WorkstationWorkdays), () => WorkstationWorkdays);
        Track(nameof(WorkstationPayday), () => WorkstationPayday);
        Track(nameof(WorkBearTextStyle), () => WorkBearTextStyle);
        Track(nameof(WaterReminderEnabled), () => WaterReminderEnabled);
        Track(nameof(WaterReminderIntervalMinutes), () => WaterReminderIntervalMinutes);
        Track(nameof(StretchReminderEnabled), () => StretchReminderEnabled);
        Track(nameof(StretchReminderIntervalMinutes), () => StretchReminderIntervalMinutes);
    }

    private void Track<T>(string name, Func<T> read)
    {
        var lastValue = read();
        var eventArgs = new PropertyChangedEventArgs(name);
        _snapshotNotifications.Add(name, () =>
        {
            var currentValue = read();
            if (EqualityComparer<T>.Default.Equals(lastValue, currentValue)) return;
            lastValue = currentValue;
            PropertyChanged?.Invoke(this, eventArgs);
        });
    }

    private void RaiseSnapshotProperties()
    {
        foreach (var notify in _snapshotNotifications.Values) notify();
    }
}
