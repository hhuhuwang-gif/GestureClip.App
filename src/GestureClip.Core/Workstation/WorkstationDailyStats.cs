namespace GestureClip.Core.Workstation;

public sealed record WorkstationDailyStats(
    DateOnly Date,
    int CopyCount = 0,
    int PasteCount = 0,
    int GestureCount = 0,
    int EstimatedSavedClicks = 0,
    int FishingMinutes = 0,
    DateTimeOffset? FishingStartedAt = null,
    int OpenClipboardCount = 0,
    int OverworkReminderCount = 0);
