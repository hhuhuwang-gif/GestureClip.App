namespace GestureClip.Core.Workstation;

public sealed record WorkstationDashboardSnapshot(
    string Title,
    string Subtitle,
    TimeSpan TimeUntilOffWork,
    decimal TodayEarned,
    decimal MonthEarned,
    int DaysUntilPayday,
    bool IsFishing,
    TimeSpan CurrentFishingDuration,
    decimal CurrentFishingValue,
    decimal TodayFishingValue,
    int CopyCount,
    int PasteCount,
    int GestureCount,
    int EstimatedSavedClicks,
    string WorkStatusText);
