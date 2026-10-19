using System.Diagnostics;
using GestureClip.Core.Clipboard;
using Microsoft.Extensions.Logging;

namespace GestureClip.Features.Clipboard;

public sealed partial class ClipboardService
{
    public async Task<ClipboardItem?> AddSnippetAsync(string text, CancellationToken cancellationToken)
    {
        var trimmed = text?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }

        var hash = _clipboardHashService.ComputeHash(trimmed);
        var existing = await _clipboardRepository.FindByHashAsync(hash, cancellationToken);
        if (existing is not null)
        {
            await _clipboardRepository.SetFavoriteAsync(existing.Id, true, cancellationToken);
            await _clipboardRepository.TouchAsync(existing.Id, cancellationToken);
            MarkHistoryChanged();
            _logger.LogInformation("Snippet already in history; marked as favorite.");
            return existing with { IsFavorite = true };
        }

        var now = DateTimeOffset.UtcNow;
        var item = new ClipboardItem(
            Guid.NewGuid(),
            "text",
            trimmed,
            CreatePreview(trimmed),
            hash,
            _clipboardHashService.ComputePlainTextHash(trimmed),
            "GestureClip",
            "gestureclip.snippet",
            false,
            true,
            false,
            0,
            now,
            now,
            null);
        await _clipboardRepository.InsertAsync(item, cancellationToken);
        MarkHistoryChanged();
        _logger.LogInformation("User snippet added as favorite.");
        return item;
    }

    public async Task<ClipboardItem?> UpdateTextContentAsync(Guid id, string text, CancellationToken cancellationToken)
    {
        var trimmed = text?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }

        var existing = await _clipboardRepository.GetByIdAsync(id, cancellationToken);
        if (existing is null || !existing.IsText)
        {
            return null;
        }

        var hash = _clipboardHashService.ComputeHash(trimmed);
        var plainTextHash = _clipboardHashService.ComputePlainTextHash(trimmed);
        var preview = CreatePreview(trimmed);
        await _clipboardRepository.UpdateTextContentAsync(id, trimmed, hash, plainTextHash, preview, cancellationToken);
        MarkHistoryChanged();
        _logger.LogInformation("Clipboard text item edited in place.");
        return existing with
        {
            TextContent = trimmed,
            PreviewText = preview,
            Hash = hash,
            PlainTextHash = plainTextHash,
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }

    public async Task<IReadOnlyList<ClipboardItem>> SearchAsync(string keyword, int limit, CancellationToken cancellationToken)
    {
        return await SearchAsync(keyword, limit, 0, cancellationToken);
    }

    public async Task<IReadOnlyList<ClipboardItem>> SearchAsync(string keyword, int limit, int offset, CancellationToken cancellationToken)
    {
        var watch = Stopwatch.StartNew();
        var results = await _clipboardRepository.SearchAsync(keyword, limit, offset, cancellationToken);
        watch.Stop();
        LogPerf("SearchMs", watch.ElapsedMilliseconds, ("ResultCount", results.Count));
        LogPerf("SearchDurationMs", watch.ElapsedMilliseconds, ("ResultCount", results.Count));
        return results;
    }

    public async Task<IReadOnlyList<ClipboardItem>> SearchAsync(
        string keyword,
        int limit,
        int offset,
        ClipboardContentFilter filter,
        CancellationToken cancellationToken)
    {
        var watch = Stopwatch.StartNew();
        var results = await _clipboardRepository.SearchAsync(keyword, limit, offset, filter, cancellationToken);
        watch.Stop();
        LogPerf("SearchMs", watch.ElapsedMilliseconds, ("ResultCount", results.Count), ("Filter", filter.ToString()));
        LogPerf("SearchDurationMs", watch.ElapsedMilliseconds, ("ResultCount", results.Count), ("Filter", filter.ToString()));
        return results;
    }

    public Task<ClipboardItem?> GetLatestAsync(CancellationToken cancellationToken)
    {
        return _clipboardRepository.GetLatestAsync(cancellationToken);
    }

    public Task<ClipboardItem?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return _clipboardRepository.GetByIdAsync(id, cancellationToken);
    }

    public async Task<int> DeleteItemsAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken)
    {
        var deleted = await _clipboardRepository.DeleteAsync(ids, cancellationToken);
        if (deleted > 0)
        {
            MarkHistoryChanged();
        }

        _logger.LogInformation("Clipboard items deleted. Count={ClipboardItemCount}", deleted);
        return deleted;
    }

    public async Task RestoreItemsAsync(IReadOnlyList<ClipboardItem> items, CancellationToken cancellationToken)
    {
        if (items.Count == 0)
        {
            return;
        }

        var restored = 0;
        foreach (var item in items)
        {
            var existing = await _clipboardRepository.GetByIdAsync(item.Id, cancellationToken);
            if (existing is not null)
            {
                continue;
            }

            await _clipboardRepository.InsertAsync(item, cancellationToken);
            restored++;
        }

        if (restored > 0)
        {
            MarkHistoryChanged();
        }

        _logger.LogInformation("Clipboard items restored. Count={ClipboardItemCount}", restored);
    }

    public async Task SetPinnedAsync(Guid id, bool isPinned, CancellationToken cancellationToken)
    {
        await _clipboardRepository.SetPinnedAsync(id, isPinned, cancellationToken);
        MarkHistoryChanged();
        _logger.LogInformation("Clipboard item pin state changed. IsPinned={IsPinned}", isPinned);
    }

    public async Task SetFavoriteAsync(Guid id, bool isFavorite, CancellationToken cancellationToken)
    {
        await _clipboardRepository.SetFavoriteAsync(id, isFavorite, cancellationToken);
        MarkHistoryChanged();
        _logger.LogInformation("Clipboard item favorite state changed. IsFavorite={IsFavorite}", isFavorite);
    }

}
