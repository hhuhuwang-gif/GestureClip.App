using GestureClip.Core.Clipboard;

namespace GestureClip.App.ViewModels;

public sealed partial class ClipboardOverlayViewModel
{
    private sealed record SearchPage(
        IReadOnlyList<ClipboardItem> Items, int PrimaryCount, bool HasMore, string Status);
    private sealed record SearchRequest(string Keyword, ClipboardContentFilter Filter, long Revision);

    public bool HasSearchError { get; private set; }

    private async Task<bool> SearchCoreAsync(CancellationToken cancellationToken)
    {
        var version = Interlocked.Increment(ref _searchVersion);
        var revision = _clipboardService.HistoryRevision;
        var request = new SearchRequest(SearchText, ToContentFilter(SelectedFilter), revision);
        DebugPerf("OVERLAY_QUERY_BEGIN");
        try
        {
            IsLoading = Items.Count == 0;
            ErrorMessage = null;
            HasSearchError = false;
            var page = await Task.Run(() => QuerySearchPageAsync(request, cancellationToken), cancellationToken);
            if (cancellationToken.IsCancellationRequested || version != Volatile.Read(ref _searchVersion))
            {
                return false;
            }

            CommitSearchPage(page, revision);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (Exception ex)
        {
            if (!cancellationToken.IsCancellationRequested && version == Volatile.Read(ref _searchVersion))
            {
                HasSearchError = true;
                ErrorMessage = $"加载剪贴板历史失败：{ex.Message}";
                StatusText = "加载失败";
            }

            return false;
        }
        finally
        {
            if (version == Volatile.Read(ref _searchVersion)) IsLoading = false;
        }
    }

    private async Task<SearchPage> QuerySearchPageAsync(SearchRequest request, CancellationToken cancellationToken)
    {
        var keyword = request.Keyword;
        var filter = request.Filter;
        if (TryGetRegexPattern(keyword, out var pattern))
        {
            var pool = await GetSmartSearchPoolAsync(request.Revision, filter, cancellationToken);
            var (matches, status) = FilterByRegex(pool, pattern, cancellationToken);
            return new SearchPage(matches, matches.Count, false, status);
        }

        var primary = await _clipboardService.SearchAsync(keyword, PageSize, 0, filter, cancellationToken);
        if (!ShouldAugmentWithPinyin(keyword, primary.Count))
        {
            return new SearchPage(primary, primary.Count, primary.Count == PageSize, "");
        }

        var candidates = await GetSmartSearchPoolAsync(request.Revision, filter, cancellationToken);
        var supplemented = AppendPinyinMatches(
            new PinyinSearchRequest(primary, candidates, keyword.Trim()), cancellationToken);
        return new SearchPage(supplemented.Items, primary.Count, primary.Count == PageSize, supplemented.Status);
    }

    private void CommitSearchPage(SearchPage page, long revision)
    {
        _lastSearchResults = page.Items.Select(WithSessionCopiedFlag).ToArray();
        _primaryResultCount = page.PrimaryCount;
        HasMoreItems = page.HasMore;
        DebugPerf("OVERLAY_QUERY_END", $"ResultCount={_lastSearchResults.Count}");
        ApplyFilter();
        StatusText = page.Status;
        _loadedHistoryRevision = revision;
        _hasLoaded = true;
    }
}
