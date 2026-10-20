using GestureClip.Core.Clipboard;
using Microsoft.Extensions.Logging;

namespace GestureClip.Features.Clipboard;

public sealed partial class PasteQueueService
{
    private async Task PasteNextAsync()
    {
        ClipboardItem item;
        long generation;
        CancellationTokenSource cancellation;
        lock (_sync)
        {
            if (_disposed || _pasting || _error is not null || !_queue.TryPeek(out item!)) return;
            _pasting = true;
            generation = _generation;
            cancellation = new();
            _activePaste = cancellation;
            ReleaseHotkeyLocked();
        }
        try
        {
            var target = _pasteTargetService.CaptureTarget();
            QueueChanged?.Invoke(this, EventArgs.Empty);
            await _pasteTargetService.WaitUntilReadyAsync(target, cancellation.Token);
            await _clipboardService.PasteAsync(item,
                new PasteOptions(false) { TargetWindowHandle = target.WindowHandle }, cancellation.Token);
            lock (_sync)
            {
                if (generation == _generation) _queue.Dequeue();
            }
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Paste queue item failed; current item retained.");
            lock (_sync)
            {
                if (generation == _generation) _error = exception.Message;
            }
        }
        finally
        {
            CompletePaste(cancellation);
        }
    }

    private void CompletePaste(CancellationTokenSource cancellation)
    {
        lock (_sync)
        {
            _activePaste = null;
            _pasting = false;
            if (!_disposed && _queue.Count > 0 && _error is null) ClaimHotkeyLocked();
        }
        cancellation.Dispose();
        QueueChanged?.Invoke(this, EventArgs.Empty);
    }
}
