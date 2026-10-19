using System.Diagnostics;
using GestureClip.Core.Clipboard;
using Microsoft.Extensions.Logging;

namespace GestureClip.Features.Clipboard;

public sealed partial class ClipboardService
{
    private void OnClipboardChanged(object? sender, ClipboardChangedEventArgs e)
    {
        DebugPerf("CAPTURE_EVENT");
        LogPerf("ListenerReceived", 0);
        if (_captureQueue is { } queue && queue.Writer.TryWrite(e))
        {
            return;
        }

        _logger.LogDebug("Clipboard capture queue unavailable or full; clipboard event dropped.");
    }

    private async Task ProcessClipboardQueueAsync(CancellationToken cancellationToken)
    {
        var reader = _captureQueue?.Reader;
        if (reader is null)
        {
            return;
        }

        try
        {
            await foreach (var item in reader.ReadAllAsync(cancellationToken))
            {
                await HandleClipboardChangedAsync(item, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Clipboard capture queue stopped unexpectedly.");
        }
    }

    private async Task HandleClipboardChangedAsync(ClipboardChangedEventArgs e)
    {
        await HandleClipboardChangedAsync(e, CancellationToken.None);
    }

    private async Task HandleClipboardChangedAsync(ClipboardChangedEventArgs e, CancellationToken cancellationToken)
    {
        try
        {
            for (var attempt = 1; ; attempt++)
            {
                DebugPerf("CAPTURE_READ_BEGIN", $"Attempt={attempt}");
                var readWatch = Stopwatch.StartNew();
                var snapshot = _clipboardTextReader.TryReadSnapshot();
                readWatch.Stop();
                LogPerf(
                    "ClipboardReadMs",
                    readWatch.ElapsedMilliseconds,
                    ("HasText", !string.IsNullOrWhiteSpace(snapshot.Text)),
                    ("HasImage", snapshot.ImagePngBase64 is not null),
                    ("PreferImage", snapshot.PreferImage));

                if (await CaptureSnapshotAsync(snapshot, e.ChangedAt, cancellationToken))
                {
                    return;
                }

                if (attempt >= MaxClipboardReadAttempts)
                {
                    DebugPerf("CAPTURE_READ_END", "HasContent=false");
                    _logger.LogDebug("Clipboard changed event produced no readable content after {Attempt} attempts.",
                        attempt);
                    return;
                }

                // Clipboard may be transiently held by another app (Excel/Chrome/screenshot tools).
                // Retry briefly; never block the UI thread and never retry forever.
                DebugPerf("CAPTURE_RETRY", $"Attempt={attempt}");
                await Task.Delay(ClipboardReadRetryDelays[attempt - 1], cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Clipboard changed handling failed.");
        }
    }


    private async Task<bool> CaptureSnapshotAsync(
        ClipboardReadSnapshot snapshot,
        DateTimeOffset changedAt,
        CancellationToken cancellationToken)
    {
        if (snapshot.PreferImage && snapshot.ImagePngBase64 is not null)
        {
            DebugPerf("CAPTURE_READ_END", "HasImage=true Preferred=true");
            await CaptureImageAsync(snapshot.ImagePngBase64, changedAt, cancellationToken);
            return true;
        }

        if (!string.IsNullOrWhiteSpace(snapshot.Text))
        {
            DebugPerf("CAPTURE_READ_END", "HasText=true");
            var foreground = _foregroundAppService.GetCurrent();
            await CaptureTextAsync(
                new ClipboardCapture(snapshot.Text, foreground.WindowTitle, foreground.ProcessName, changedAt),
                cancellationToken);
            return true;
        }

        if (snapshot.ImagePngBase64 is not null)
        {
            DebugPerf("CAPTURE_READ_END", "HasImage=true Preferred=false");
            await CaptureImageAsync(snapshot.ImagePngBase64, changedAt, cancellationToken);
            return true;
        }

        return false;
    }
}
