using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using GestureClip.Core.Abstractions;
using GestureClip.Core.Clipboard;
using GestureClip.Features.Assistant;
using GestureClip.Features.Clipboard;

namespace GestureClip.App.ViewModels;


public sealed partial class ClipboardOverlayViewModel
{
    public async Task<bool> LoadMoreAsync()
    {
        if (!CanLoadMore)
        {
            return false;
        }

        var version = Volatile.Read(ref _searchVersion);
        var cancellation = _searchCancellation?.Token ?? CancellationToken.None;
        var offset = _primaryResultCount;
        try
        {
            IsLoadingMore = true;
            ErrorMessage = null;
            var keyword = SearchText;
            var filter = ToContentFilter(SelectedFilter);
            var nextPage = await Task.Run(() => _clipboardService.SearchAsync(
                keyword, PageSize, offset, filter, cancellation), cancellation);
            if (cancellation.IsCancellationRequested || version != Volatile.Read(ref _searchVersion))
            {
                return false;
            }

            var existingIds = _lastSearchResults.Select(item => item.Id).ToHashSet();
            var additions = nextPage.Where(item => existingIds.Add(item.Id))
                .Select(WithSessionCopiedFlag).ToArray();
            _lastSearchResults = _lastSearchResults.Concat(additions).ToArray();
            _primaryResultCount += nextPage.Count;
            HasMoreItems = nextPage.Count == PageSize;
            AppendVisibleItems(additions);
            StatusText = nextPage.Count == 0 ? "没有更多记录了" : $"已加载 {nextPage.Count} 条";
            return nextPage.Count > 0;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"加载更多剪贴板历史失败：{ex.Message}";
            StatusText = "加载更多失败";
            return false;
        }
        finally
        {
            IsLoadingMore = false;
        }
    }

    private void AppendVisibleItems(IReadOnlyList<ClipboardItem> additions)
    {
        foreach (var item in additions.Where(MatchesSelectedFilter))
        {
            Items.Add(item);
        }

        if (SelectedItem is null) SelectLatestItem();
        OnPropertyChanged(nameof(SummaryText));
        OnPropertyChanged(nameof(HasItems));
        OnPropertyChanged(nameof(IsEmpty));
    }

    private void ApplyFilter(bool keepSelection = false)
    {
        var refreshWatch = Stopwatch.StartNew();
        var previousSelectedId = keepSelection ? SelectedItem?.Id : null;
        var filtered = _lastSearchResults.Where(MatchesSelectedFilter).ToArray();
        Items.ReplaceRange(filtered);

        SelectedItem = previousSelectedId is { } id
            ? Items.FirstOrDefault(item => item.Id == id) ?? Items.FirstOrDefault()
            : Items.FirstOrDefault();
        UpdateSelectedCount(SelectedItem is null ? 0 : 1);
        EmptyStateText = Items.Count == 0 ? "没有匹配的剪贴板记录" : "";
        OnPropertyChanged(nameof(SummaryText));
        OnPropertyChanged(nameof(HasItems));
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(EmptyStateTitle));
        OnPropertyChanged(nameof(EmptyStateHint));
        refreshWatch.Stop();
        Trace.WriteLine($"ClipboardPerf UiRefreshDurationMs ElapsedMs={refreshWatch.ElapsedMilliseconds} ItemCount={Items.Count} Filter={SelectedFilter}");
        DebugPerf("OVERLAY_ITEMS_COMMIT", $"ItemCount={Items.Count} ElapsedMs={refreshWatch.ElapsedMilliseconds}");
    }

    public void UpdateSelectedCount(int selectedCount)
    {
        if (_selectedCount == selectedCount)
        {
            return;
        }

        _selectedCount = selectedCount;
        OnPropertyChanged(nameof(SummaryText));
    }

    private bool MatchesSelectedFilter(ClipboardItem item)
    {
        return SelectedFilter switch
        {
            ClipboardOverlayFilter.Pinned => item.IsPinned,
            ClipboardOverlayFilter.Favorites => item.IsFavorite,
            ClipboardOverlayFilter.Text => item.IsText,
            ClipboardOverlayFilter.Images => item.IsImage,
            ClipboardOverlayFilter.Links => IsLinkItem(item),
            _ => true
        };
    }

    private static bool IsLinkItem(ClipboardItem item)
    {
        if (!item.IsText)
        {
            return false;
        }

        var text = item.TextContent ?? item.PreviewText ?? "";
        return text.Contains("://", StringComparison.Ordinal) ||
            text.StartsWith("www.", StringComparison.OrdinalIgnoreCase);
    }

    private static ClipboardContentFilter ToContentFilter(ClipboardOverlayFilter filter)
    {
        return filter switch
        {
            ClipboardOverlayFilter.Pinned => ClipboardContentFilter.Pinned,
            ClipboardOverlayFilter.Favorites => ClipboardContentFilter.Favorites,
            ClipboardOverlayFilter.Text => ClipboardContentFilter.Text,
            ClipboardOverlayFilter.Images => ClipboardContentFilter.Images,
            ClipboardOverlayFilter.Links => ClipboardContentFilter.Links,
            _ => ClipboardContentFilter.All
        };
    }

    private CancellationTokenSource ReplaceSearchCancellation()
    {
        var next = new CancellationTokenSource();
        var previous = Interlocked.Exchange(ref _searchCancellation, next);
        if (previous is not null)
        {
            previous.Cancel();
            previous.Dispose();
        }

        return next;
    }

}
