using GestureClip.Core.Clipboard;

namespace GestureClip.App.ViewModels;

public sealed partial class ClipboardOverlayViewModel
{
    private sealed record SearchPage(
        IReadOnlyList<ClipboardItem> Items, int PrimaryCount, bool HasMore, string Status)
    {
        public bool HasError { get; init; }
    }
    private sealed record SearchRequest(string Keyword, ClipboardContentFilter Filter, long Revision)
    {
        public IProgress<int>? Progress { get; init; }
    }
    private int _smartSearchProgressVersion = -1;

    public bool HasSearchError { get; private set; }

    private async Task<bool> SearchCoreAsync(CancellationToken cancellationToken)
    {
        var version = Interlocked.Increment(ref _searchVersion);
        var revision = _clipboardService.HistoryRevision;
        var request = new SearchRequest(SearchText, ToContentFilter(SelectedFilter), revision)
        {
            Progress = CreateSearchProgress(version, cancellationToken)
        };
        DebugPerf("OVERLAY_QUERY_BEGIN");
        try
        {
            PrepareSearchState(request.Keyword);
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
            if (version == Volatile.Read(ref _searchVersion))
            {
                _smartSearchProgressVersion = -1;
                IsLoading = false;
            }
        }
    }

    private void PrepareSearchState(string keyword)
    {
        IsLoading = Items.Count == 0;
        ErrorMessage = null;
        HasSearchError = false;
        if (TryGetRegexPattern(keyword, out _) || ShouldAugmentWithPinyin(keyword))
        {
            StatusText = "正在搜索全部历史";
        }
    }

    private async Task<SearchPage> QuerySearchPageAsync(SearchRequest request, CancellationToken cancellationToken)
    {
        var keyword = request.Keyword;
        var filter = request.Filter;
        if (TryGetRegexPattern(keyword, out var pattern))
        {
            return await QueryRegexSearchAsync(request, pattern, cancellationToken);
        }

        var primary = await _clipboardService.SearchAsync(keyword, PageSize, 0, filter, cancellationToken);
        if (!ShouldAugmentWithPinyin(keyword))
        {
            return new SearchPage(primary, primary.Count, primary.Count == PageSize, "");
        }

        var candidates = await GetSmartSearchPoolAsync(
            new SmartSearchPoolRequest(request.Revision, filter, request.Progress), cancellationToken);
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
        HasSearchError = page.HasError;
        ErrorMessage = page.HasError ? page.Status : null;
        _loadedHistoryRevision = revision;
        _hasLoaded = !page.HasError;
    }

    private IProgress<int> CreateSearchProgress(int version, CancellationToken cancellationToken)
    {
        _smartSearchProgressVersion = version;
        return new Progress<int>(count =>
        {
            if (!cancellationToken.IsCancellationRequested &&
                version == Volatile.Read(ref _searchVersion) && _smartSearchProgressVersion == version)
            {
                StatusText = $"正在搜索全部历史 · 已扫描 {count} 条";
            }
        });
    }
}
