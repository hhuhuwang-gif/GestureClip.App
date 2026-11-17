using GestureClip.Core.Abstractions;
using GestureClip.Core.Workstation;

namespace GestureClip.Tests.Workstation;

public sealed partial class WorkstationDashboardViewModelTests
{
    private sealed class ManualTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => Now;
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }

    private sealed class FakeStatsRepository : IWorkstationStatsRepository
    {
        public IReadOnlyList<WorkstationDailyStats> Days { get; set; } = [];
        public Exception? Error { get; set; }
        public int ReadCount { get; private set; }

        public Task<IReadOnlyList<WorkstationDailyStats>> GetRangeAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken)
        {
            ReadCount++;
            return Error is null
                ? Task.FromResult(Days)
                : Task.FromException<IReadOnlyList<WorkstationDailyStats>>(Error);
        }

        public Task<WorkstationDailyStats> GetOrCreateAsync(DateOnly date, CancellationToken cancellationToken) =>
            throw new NotSupportedException("The dashboard must use the range query.");

        public Task SaveAsync(WorkstationDailyStats stats, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task ResetAsync(DateOnly date, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task IncrementCountersAsync(DateOnly date, int copyDelta, int pasteDelta, int gestureDelta, int savedClicksDelta, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
