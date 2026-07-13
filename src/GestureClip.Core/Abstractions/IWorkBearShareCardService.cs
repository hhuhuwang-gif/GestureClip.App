namespace GestureClip.Core.Abstractions;

public interface IWorkBearShareCardService
{
    Task<string> GenerateTodayCardAsync(CancellationToken cancellationToken);

    Task<string> GenerateTodayCardAsync(string style, CancellationToken cancellationToken) =>
        GenerateTodayCardAsync(cancellationToken);

    void OpenCardFolder(string cardPath);
}
