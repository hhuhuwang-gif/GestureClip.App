using System.Text.RegularExpressions;
using GestureClip.Core.Clipboard;
using GestureClip.Features.Clipboard;

namespace GestureClip.App.ViewModels;

public sealed partial class ClipboardOverlayViewModel
{
    private const int RegexSearchTimeoutMilliseconds = 250;
    private sealed record SmartSearchPool(
        long Revision, ClipboardContentFilter Filter, IReadOnlyList<ClipboardSearchEntry> Items);

    private SmartSearchPool? _smartSearchPool;
    private readonly object _smartSearchCacheSync = new();
    private int _smartSearchPoolVersion;
    private sealed record PinyinSearchRequest(
        IReadOnlyList<ClipboardItem> Primary, IReadOnlyList<ClipboardSearchEntry> Pool, string Keyword)
    {
        public long Revision { get; init; }
    }
    private sealed record SmartSearchPoolRequest(long Revision, ClipboardContentFilter Filter, IProgress<int>? Progress);

    private async Task<IReadOnlyList<ClipboardSearchEntry>> GetSmartSearchPoolAsync(
        SmartSearchPoolRequest request, CancellationToken cancellationToken)
    {
        int cacheVersion;
        lock (_smartSearchCacheSync)
        {
            if (_smartSearchPool is { } cached && cached.Revision == request.Revision && cached.Filter == request.Filter)
            {
                return cached.Items;
            }
            _smartSearchPool = null;
            cacheVersion = _smartSearchPoolVersion;
        }

        var pool = new List<ClipboardSearchEntry>();
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            EnsureSearchRevision(request.Revision);
            var batch = await _clipboardService.ReadSearchIndexAsync(
                new ClipboardSearchIndexPage(SmartSearchBatchSize, pool.Count, request.Filter), cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            pool.AddRange(batch);
            request.Progress?.Report(pool.Count);
            if (batch.Count < SmartSearchBatchSize) break;
        }

        EnsureSearchRevision(request.Revision);
        var entries = pool.ToArray();
        lock (_smartSearchCacheSync)
        {
            if (cacheVersion == _smartSearchPoolVersion)
            {
                _smartSearchPool = new SmartSearchPool(request.Revision, request.Filter, entries);
            }
        }
        return entries;
    }

    /// <summary>Releases the reusable matching pool, preserving the current results and search state.</summary>
    public void ReleaseSearchCache()
    {
        lock (_smartSearchCacheSync)
        {
            _smartSearchPoolVersion++;
            _smartSearchPool = null;
        }
    }

    private void EnsureSearchRevision(long revision)
    {
        if (_clipboardService.HistoryRevision != revision)
        {
            throw new InvalidOperationException("搜索期间剪贴板历史已变更，请重新搜索");
        }
    }

    private async Task<(IReadOnlyList<ClipboardItem> Items, string Status)> AppendPinyinMatchesAsync(
        PinyinSearchRequest request, CancellationToken cancellationToken)
    {
        var knownIds = request.Primary.Select(item => item.Id).ToHashSet();
        var matches = new List<Guid>();
        foreach (var item in request.Pool)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!knownIds.Contains(item.Id) && !MatchesLiteralKeyword(item, request.Keyword) &&
                MatchesPinyinInitials(item, request.Keyword))
            {
                knownIds.Add(item.Id);
                matches.Add(item.Id);
            }
        }

        var displayItems = await ReadSearchMatchesAsync(matches, request.Revision, cancellationToken);
        return (request.Primary.Concat(displayItems).ToArray(),
            $"已搜索全部 {request.Pool.Count} 条历史 · 含 {matches.Count} 条拼音首字母匹配");
    }

    private static bool TryGetRegexPattern(string keyword, out string pattern)
    {
        pattern = "";
        var trimmed = keyword.Trim();
        if (!trimmed.StartsWith(RegexSearchPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        pattern = trimmed[RegexSearchPrefix.Length..].Trim();
        return pattern.Length > 0;
    }

    private async Task<SearchPage> QueryRegexSearchAsync(
        SearchRequest request, string pattern, CancellationToken cancellationToken)
    {
        Regex regex;
        try
        {
            regex = new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
                TimeSpan.FromMilliseconds(RegexSearchTimeoutMilliseconds));
        }
        catch (ArgumentException)
        {
            return new SearchPage([], 0, false, "正则表达式无效，请检查 re: 后面的内容") { HasError = true };
        }

        var pool = await GetSmartSearchPoolAsync(
            new SmartSearchPoolRequest(request.Revision, request.Filter, request.Progress), cancellationToken);
        var (matches, status, hasError) = FilterByRegex(pool, regex, cancellationToken);
        IReadOnlyList<ClipboardItem> displayItems = hasError
            ? []
            : await ReadSearchMatchesAsync(matches, request.Revision, cancellationToken);
        return new SearchPage(displayItems, displayItems.Count, false, status) { HasError = hasError };
    }

    private static (IReadOnlyList<Guid> Results, string Status, bool HasError) FilterByRegex(
        IReadOnlyList<ClipboardSearchEntry> pool, Regex regex, CancellationToken cancellationToken)
    {
        var matches = new List<Guid>();
        foreach (var item in pool)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                if (MatchesRegex(item.TextContent, regex) || MatchesRegex(item.PreviewText, regex) ||
                    MatchesRegex(item.OcrText, regex)) matches.Add(item.Id);
            }
            catch (RegexMatchTimeoutException)
            {
                return ([], "正则匹配超时，请简化表达式后重试", true);
            }
        }

        return (matches, $"已搜索全部 {pool.Count} 条历史 · 正则匹配 {matches.Count} 条", false);
    }

    private static bool MatchesRegex(string? text, Regex regex) => !string.IsNullOrEmpty(text) && regex.IsMatch(text);

    private static bool ShouldAugmentWithPinyin(string keyword)
    {
        const int minimumInitialsLength = 2;
        const int maximumInitialsLength = 12;
        var trimmed = keyword.Trim();
        return trimmed.Length >= minimumInitialsLength && trimmed.Length <= maximumInitialsLength &&
            trimmed.All(char.IsAsciiLetter);
    }

    private static bool MatchesPinyinInitials(ClipboardSearchEntry item, string keyword)
    {
        return PinyinInitialMatcher.Matches(item.PreviewText, keyword) ||
            PinyinInitialMatcher.Matches(item.TextContent, keyword) ||
            PinyinInitialMatcher.Matches(item.OcrText, keyword);
    }

    private static bool MatchesLiteralKeyword(ClipboardSearchEntry item, string keyword)
    {
        return (item.IsText && item.TextContent?.Contains(keyword, StringComparison.OrdinalIgnoreCase) == true) ||
            item.PreviewText?.Contains(keyword, StringComparison.OrdinalIgnoreCase) == true ||
            item.OcrText?.Contains(keyword, StringComparison.OrdinalIgnoreCase) == true;
    }

    private async Task<IReadOnlyList<ClipboardItem>> ReadSearchMatchesAsync(
        IReadOnlyList<Guid> ids, long revision, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureSearchRevision(revision);
        var matches = await _clipboardService.GetDisplayItemsAsync(ids, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        EnsureSearchRevision(revision);
        if (matches.Count != ids.Count)
        {
            throw new InvalidOperationException("搜索命中的剪贴板记录已变更，请重新搜索");
        }

        return matches;
    }
}
