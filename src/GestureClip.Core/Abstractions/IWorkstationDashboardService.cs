using GestureClip.Core.Workstation;

namespace GestureClip.Core.Abstractions;

public interface IWorkstationDashboardService
{
    Task<WorkstationDashboardSnapshot> GetSnapshotAsync(DateTimeOffset now, CancellationToken cancellationToken);

    Task StartFishingAsync(DateTimeOffset now, CancellationToken cancellationToken);

    Task EndFishingAsync(DateTimeOffset now, CancellationToken cancellationToken);

    Task ResetTodayAsync(DateOnly date, CancellationToken cancellationToken);

    Task RecordCopyAsync(DateTimeOffset now, CancellationToken cancellationToken);

    Task RecordPasteAsync(DateTimeOffset now, CancellationToken cancellationToken);

    Task RecordGestureAsync(DateTimeOffset now, CancellationToken cancellationToken);

    Task RecordClipboardOpenAsync(DateTimeOffset now, CancellationToken cancellationToken) => Task.CompletedTask;

    Task RecordOverworkReminderAsync(DateTimeOffset now, CancellationToken cancellationToken) => Task.CompletedTask;

    Task SetSprintModeAsync(bool enabled, CancellationToken cancellationToken) => Task.CompletedTask;

    Task ClearTodayFishingAsync(DateOnly date, CancellationToken cancellationToken) => Task.CompletedTask;

    Task<WorkBearDailyReport> GenerateDailyReportAsync(DateTimeOffset now, CancellationToken cancellationToken) =>
        Task.FromResult(new WorkBearDailyReport(DateOnly.FromDateTime(now.Date), TimeSpan.Zero, TimeSpan.Zero, 0, TimeSpan.Zero, 0, 0, 0, 0, 0, 0, 0, TimeSpan.Zero, "", "", ""));
}
