using GestureClip.Core.Clipboard;

namespace GestureClip.Core.Abstractions;

public interface IClipboardService
{
    bool IsCaptureEnabled { get; }

    DateTimeOffset? SuppressCaptureUntil { get; }

    Task StartAsync(CancellationToken cancellationToken);

    Task StopAsync(CancellationToken cancellationToken);

    Task SetCaptureEnabledAsync(bool enabled, CancellationToken cancellationToken);

    void SuppressCaptureFor(TimeSpan duration);

    Task CaptureTextAsync(ClipboardCapture capture, CancellationToken cancellationToken);

    Task<IReadOnlyList<ClipboardItem>> SearchAsync(string keyword, int limit, CancellationToken cancellationToken);

    Task<ClipboardItem?> GetLatestAsync(CancellationToken cancellationToken);

    Task PasteAsync(ClipboardItem item, PasteOptions options, CancellationToken cancellationToken);
}
