namespace GestureClip.Core.Abstractions;

public interface IUpdateInstallerService
{
    Task StartCoverUpdateAsync(CancellationToken cancellationToken = default);
}
