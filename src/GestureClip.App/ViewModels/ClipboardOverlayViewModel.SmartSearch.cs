using System.Text.RegularExpressions;
using GestureClip.Core.Clipboard;
using GestureClip.Features.Clipboard;

namespace GestureClip.App.ViewModels;

public sealed partial class ClipboardOverlayViewModel
{
    private const int RegexSearchTimeoutMilliseconds = 250;
    private sealed record SmartSearchPool(
        long Revision, ClipboardContentFilter Filter, IReadOnlyList<ClipboardItem> Items);

    private SmartSearchPool? _smartSearchPool;
    private sealed record PinyinSearchRequest(
        IReadOnlyList<ClipboardItem> Primary, IReadOnlyList<ClipboardItem> Pool, string Keyword);
    private sealed record SmartSearchPoolRequest(long Revision, ClipboardContentFilter Filter, IProgress<int>? Progress);

    private async Task<IReadOnlyList<ClipboardItem>> GetSmartSearchPoolAsync(
        SmartSearchPoolRequest request, CancellationToken cancellationToken)
    {
        if (_smartSearchPool is { } cached && cached.Revision == request.Revision && cached.Filter == request.Filter)
        {
            return cached.Items;
        }

        var pool = new List<ClipboardItem>();
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            EnsureSearchRevision(request.Revision);
            var batch = await _clipboardService.SearchAsync(
                "", SmartSearchBatchSize, pool.Count, request.Filter, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            pool.AddRange(batch);
            request.Progress?.Report(pool.Count);
            if (batch.Count < SmartSearchBatchSize) break;
        }

        EnsureSearchRevision(request.Revision);
        _smartSearchPool = new SmartSearchPool(request.Revision, request.Filter, pool.ToArray());
        return _smartSearchPool.Items;
    }

    private void EnsureSearchRevision(long revision)
    {
        if (_clipboardService.HistoryRevision != revision)
        {
            throw new InvalidOperationException("搜索期间剪贴板历史已变更，请重新搜索");
        }
    }

    private static (IReadOnlyList<ClipboardItem> Items, string Status) AppendPinyinMatches(
        PinyinSearchRequest request, CancellationToken cancellationToken)
    {
        var knownIds = request.Primary.Select(item => item.Id).ToHashSet();
        var matches = new List<ClipboardItem>();
        foreach (var item in request.Pool)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!knownIds.Contains(item.Id) && !MatchesLiteralKeyword(item, request.Keyword) &&
                MatchesPinyinInitials(item, request.Keyword))
            {
                knownIds.Add(item.Id);
                matches.Add(item);
            }
        }

        return (request.Primary.Concat(matches).ToArray(),
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
        return new SearchPage(matches, matches.Count, false, status) { HasError = hasError };
    }

    private static (IReadOnlyList<ClipboardItem> Results, string Status, bool HasError) FilterByRegex(
        IReadOnlyList<ClipboardItem> pool, Regex regex, CancellationToken cancellationToken)
    {
        var matches = new List<ClipboardItem>();
        foreach (var item in pool)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var texts = new[] { item.TextContent, item.PreviewText, item.OcrText };
                if (texts.Any(text => !string.IsNullOrEmpty(text) && regex.IsMatch(text))) matches.Add(item);
            }
            catch (RegexMatchTimeoutException)
            {
                return ([], "正则匹配超时，请简化表达式后重试", true);
            }
        }

        return (matches, $"已搜索全部 {pool.Count} 条历史 · 正则匹配 {matches.Count} 条", false);
    }

    private static bool ShouldAugmentWithPinyin(string keyword)
    {
        const int minimumInitialsLength = 2;
        const int maximumInitialsLength = 12;
        var trimmed = keyword.Trim();
        return trimmed.Length >= minimumInitialsLength && trimmed.Length <= maximumInitialsLength &&
            trimmed.All(char.IsAsciiLetter);
    }

    private static bool MatchesPinyinInitials(ClipboardItem item, string keyword)
    {
        return PinyinInitialMatcher.Matches(item.PreviewText, keyword) ||
            PinyinInitialMatcher.Matches(item.TextContent, keyword) ||
            PinyinInitialMatcher.Matches(item.OcrText, keyword);
    }

    private static bool MatchesLiteralKeyword(ClipboardItem item, string keyword)
    {
        return (item.IsText && item.TextContent?.Contains(keyword, StringComparison.OrdinalIgnoreCase) == true) ||
            item.PreviewText?.Contains(keyword, StringComparison.OrdinalIgnoreCase) == true ||
            item.OcrText?.Contains(keyword, StringComparison.OrdinalIgnoreCase) == true;
    }
}
