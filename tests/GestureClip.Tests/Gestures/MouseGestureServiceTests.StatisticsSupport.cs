using GestureClip.Core.Abstractions;
using GestureClip.Core.Gestures;
using GestureClip.Features.Gestures;
using Microsoft.Extensions.Logging.Abstractions;
using System.Collections.Concurrent;
using Xunit;

namespace GestureClip.Tests.Gestures;

public sealed partial class MouseGestureServiceTests
{
    private sealed class FakeWorkstationDashboardService : IWorkstationDashboardService
    {
        public int GestureCount { get; private set; }

        public TimeSpan RecordGestureDelay { get; init; }

        public Task<Core.Workstation.WorkstationDashboardSnapshot> GetSnapshotAsync(DateTimeOffset now, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task StartFishingAsync(DateTimeOffset now, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task EndFishingAsync(DateTimeOffset now, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task ResetTodayAsync(DateOnly date, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task RecordCopyAsync(DateTimeOffset now, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task RecordPasteAsync(DateTimeOffset now, CancellationToken cancellationToken) => Task.CompletedTask;

        public async Task RecordGestureAsync(DateTimeOffset now, CancellationToken cancellationToken)
        {
            if (RecordGestureDelay > TimeSpan.Zero)
            {
                await Task.Delay(RecordGestureDelay, cancellationToken);
            }

            GestureCount++;
        }
    }

    private sealed class FakeWorkerLevelUpService : IWorkerLevelUpService
    {
        public int ShowCount { get; private set; }

        public Task ShowLevelUpAsync(Core.WorkerLevel.WorkerLevelSnapshot snapshot, CancellationToken cancellationToken)
        {
            ShowCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeWorkerLevelService : IWorkerLevelService
    {
        public List<BuiltInGestureAction> RecordedActions { get; } = [];

        public bool LastGestureSuccess { get; private set; }

        public bool ThrowOnRecord { get; set; }

        public bool LeveledUp { get; set; }

        public TimeSpan RecordDelay { get; init; }

        public Task<Core.WorkerLevel.WorkerLevelSnapshot> GetSnapshotAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(new Core.WorkerLevel.WorkerLevelSnapshot(
                0,
                0,
                new Core.WorkerLevel.WorkerLevelDefinition(1, 0, "初入工位"),
                new Core.WorkerLevel.WorkerLevelDefinition(2, 50, "复制学徒"),
                0,
                50,
                0,
                LeveledUp,
                1,
                null));
        }

        public Task<Core.WorkerLevel.WorkerLevelSnapshot> RecordBonusXpAsync(int xp, DateTimeOffset now, CancellationToken cancellationToken)
        {
            return GetSnapshotAsync(cancellationToken);
        }

        public Task<Core.WorkerLevel.WorkerLevelSnapshot> RecordActionAsync(BuiltInGestureAction action, bool isGestureSuccess, DateTimeOffset now, CancellationToken cancellationToken)
        {
            if (ThrowOnRecord)
            {
                throw new InvalidOperationException("xp boom");
            }

            if (RecordDelay > TimeSpan.Zero)
            {
                return RecordActionWithDelayAsync(action, isGestureSuccess, cancellationToken);
            }

            RecordedActions.Add(action);
            LastGestureSuccess = isGestureSuccess;
            return GetSnapshotAsync(cancellationToken);
        }

        private async Task<Core.WorkerLevel.WorkerLevelSnapshot> RecordActionWithDelayAsync(
            BuiltInGestureAction action,
            bool isGestureSuccess,
            CancellationToken cancellationToken)
        {
            await Task.Delay(RecordDelay, cancellationToken);
            RecordedActions.Add(action);
            LastGestureSuccess = isGestureSuccess;
            return await GetSnapshotAsync(cancellationToken);
        }
    }
}
