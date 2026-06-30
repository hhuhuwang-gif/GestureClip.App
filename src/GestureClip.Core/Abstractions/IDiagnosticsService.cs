using GestureClip.Core.Diagnostics;

namespace GestureClip.Core.Abstractions;

public interface IDiagnosticsService
{
    Task<DiagnosticsSnapshot> GetSnapshotAsync(CancellationToken cancellationToken);

    Task<string> BuildReportAsync(CancellationToken cancellationToken);
}
