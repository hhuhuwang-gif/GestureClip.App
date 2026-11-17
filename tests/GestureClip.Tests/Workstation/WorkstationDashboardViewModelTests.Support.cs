using GestureClip.App.ViewModels;
using GestureClip.Core.Abstractions;
using GestureClip.Core.Settings;
using GestureClip.Core.Workstation;
using Xunit;

namespace GestureClip.Tests.Workstation;

public sealed partial class WorkstationDashboardViewModelTests
{
    private static readonly WorkstationDashboardSnapshot Snapshot = new(
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
        "开机缓冲期");

    private sealed class FakeWorkstationDashboardService : IWorkstationDashboardService
    {
        public WorkstationDashboardSnapshot Snapshot { get; set; } = WorkstationDashboardViewModelTests.Snapshot;
        public Func<Task<WorkstationDashboardSnapshot>>? SnapshotRequest { get; set; }
        public int RefreshCount { get; private set; }
        public int StartFishingCount { get; private set; }
        public int EndFishingCount { get; private set; }
        public int ResetCount { get; private set; }
        public DateOnly? LastResetDate { get; private set; }

        public Task<WorkstationDashboardSnapshot> GetSnapshotAsync(DateTimeOffset now, CancellationToken cancellationToken)
        {
            RefreshCount++;
            return SnapshotRequest?.Invoke() ?? Task.FromResult(Snapshot);
        }

        public Task StartFishingAsync(DateTimeOffset now, CancellationToken cancellationToken)
        {
            StartFishingCount++;
            return Task.CompletedTask;
        }

        public Task EndFishingAsync(DateTimeOffset now, CancellationToken cancellationToken)
        {
            EndFishingCount++;
            return Task.CompletedTask;
        }

        public Task ResetTodayAsync(DateOnly date, CancellationToken cancellationToken)
        {
            LastResetDate = date;
            ResetCount++;
            return Task.CompletedTask;
        }

        public Task RecordCopyAsync(DateTimeOffset now, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task RecordPasteAsync(DateTimeOffset now, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task RecordGestureAsync(DateTimeOffset now, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakeSettingsService : ISettingsService
    {
        public Dictionary<string, object?> Values { get; } = new();

        public T Get<T>(string key, T defaultValue) => Values.TryGetValue(key, out var value) ? (T)value! : defaultValue;

        public Task SetAsync<T>(string key, T value, CancellationToken cancellationToken)
        {
            Values[key] = value;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeConfirmationService : IConfirmationService
    {
        public bool Result { get; set; } = true;
        public int CallCount { get; private set; }
        public string? LastTitle { get; private set; }
        public string? LastMessageText { get; private set; }

        public bool Confirm(string title, string message)
        {
            CallCount++;
            LastTitle = title;
            LastMessageText = message;
            return Result;
        }
    }
}
