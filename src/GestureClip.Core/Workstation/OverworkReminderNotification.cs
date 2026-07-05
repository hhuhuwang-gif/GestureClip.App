namespace GestureClip.Core.Workstation;

public sealed record OverworkReminderNotification(
    string Title,
    string Message,
    string Detail,
    WorkTimeStage Stage,
    bool CanSnooze);
