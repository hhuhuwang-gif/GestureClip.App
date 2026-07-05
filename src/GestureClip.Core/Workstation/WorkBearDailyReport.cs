namespace GestureClip.Core.Workstation;

public sealed record WorkBearDailyReport(
    DateOnly Date,
    TimeSpan WorkDuration,
    TimeSpan EffectiveWorkDuration,
    decimal TodayEarned,
    TimeSpan FishingDuration,
    decimal FishingValue,
    int CopyCount,
    int PasteCount,
    int GestureCount,
    int EstimatedSavedClicks,
    int OpenClipboardCount,
    int OverworkReminderCount,
    TimeSpan OvertimeDuration,
    string Rating,
    string BearLine,
    string ReportText);
