using System.Diagnostics;
using GestureClip.Core.Clipboard;
using Microsoft.Extensions.Logging;

namespace GestureClip.Features.Clipboard;

public sealed partial class ClipboardService
{
    private void RecordCopyInBackground(DateTimeOffset now)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await _workstationDashboardService.RecordCopyAsync(now, CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Clipboard copy stats recording failed.");
            }
        });
    }

    private async Task<ClipboardItem> EnsureFullContentAsync(ClipboardItem item, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(item.TextContent))
        {
            return item;
        }

        var fullItem = await _clipboardRepository.GetByIdAsync(item.Id, cancellationToken);
        return fullItem ?? item;
    }

    private async Task RecordPasteUsageAsync(Guid itemId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        RecordUseCountInBackground([itemId]);
        await RecordPasteStatsAsync(now, cancellationToken);
    }

    private void RecordUseCountInBackground(IReadOnlyList<Guid> itemIds)
    {
        if (itemIds.Count == 0)
        {
            return;
        }

        lock (_usageSync)
        {
            foreach (var itemId in itemIds)
            {
                _pendingUseCounts[itemId] = _pendingUseCounts.TryGetValue(itemId, out var count)
                    ? count + 1
                    : 1;
            }

            if (_usageFlushTask is not null && !_usageFlushTask.IsCompleted)
            {
                return;
            }

            _usageFlushTask = Task.Run(DelayedFlushUseCountsAsync);
        }
    }

    private async Task DelayedFlushUseCountsAsync()
    {
        try
        {
            await Task.Delay(250);
            await FlushPendingUseCountsAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Clipboard use count batch recording failed.");
        }
    }

    private async Task FlushPendingUseCountsAsync(CancellationToken cancellationToken)
    {
        Dictionary<Guid, int> snapshot;
        lock (_usageSync)
        {
            if (_pendingUseCounts.Count == 0)
            {
                return;
            }

            snapshot = new Dictionary<Guid, int>(_pendingUseCounts);
            _pendingUseCounts.Clear();
        }

        try
        {
            var watch = Stopwatch.StartNew();
            await _clipboardRepository.IncrementUseCountsAsync(snapshot, cancellationToken);
            MarkHistoryChanged();
            watch.Stop();
            LogPerf("DbUpdateDurationMs", watch.ElapsedMilliseconds, ("Operation", "IncrementUseCounts"), ("ItemCount", snapshot.Count));
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Clipboard use count batch recording failed.");
        }
    }

    private async Task RecordPasteStatsAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        try
        {
            await _workstationDashboardService.RecordPasteAsync(now, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Clipboard paste stats recording failed.");
        }
    }

}
