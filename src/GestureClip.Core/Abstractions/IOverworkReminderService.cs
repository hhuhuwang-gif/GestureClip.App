namespace GestureClip.Core.Abstractions;

public interface IOverworkReminderService
{
    Task StartAsync(CancellationToken cancellationToken);

    Task StopAsync(CancellationToken cancellationToken);

    Task CheckNowAsync(DateTimeOffset now, CancellationToken cancellationToken);

    Task SnoozeAsync(DateTimeOffset now, CancellationToken cancellationToken) => Task.CompletedTask;

    Task MuteTodayAsync(DateTimeOffset now, CancellationToken cancellationToken) => Task.CompletedTask;
}
