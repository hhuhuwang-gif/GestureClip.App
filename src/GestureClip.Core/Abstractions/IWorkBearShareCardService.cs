namespace GestureClip.Core.Abstractions;

public interface IWorkBearShareCardService
{
    Task<string> GenerateTodayCardAsync(CancellationToken cancellationToken);
}
