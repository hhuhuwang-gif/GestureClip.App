namespace GestureClip.Core.Abstractions;

/// <summary>Gentle water / stretch reminders during work stages. Both types are opt-in.</summary>
public interface IWellnessReminderService
{
    Task StartAsync(CancellationToken cancellationToken);

    Task StopAsync(CancellationToken cancellationToken);

    Task CheckNowAsync(DateTimeOffset now, CancellationToken cancellationToken);
}
