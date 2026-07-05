using GestureClip.Core.Workstation;

namespace GestureClip.Core.Abstractions;

public interface IOverworkReminderToastService
{
    Task<OverworkReminderToastResult> ShowAsync(OverworkReminderNotification notification, CancellationToken cancellationToken);
}
