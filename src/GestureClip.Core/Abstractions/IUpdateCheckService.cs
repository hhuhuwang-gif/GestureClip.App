using GestureClip.Core.Updates;

namespace GestureClip.Core.Abstractions;

public interface IUpdateCheckService
{
    Task<UpdateCheckResult> CheckLatestAsync(CancellationToken cancellationToken = default);
}
