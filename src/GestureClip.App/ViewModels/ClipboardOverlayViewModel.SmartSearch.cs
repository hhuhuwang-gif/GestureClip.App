using System.Text.RegularExpressions;
using GestureClip.Core.Clipboard;
using GestureClip.Features.Clipboard;

namespace GestureClip.App.ViewModels;

public sealed partial class ClipboardOverlayViewModel
{
    private sealed record SmartSearchPool(
        long Revision, ClipboardContentFilter Filter, IReadOnlyList<ClipboardItem> Items);

    private SmartSearchPool? _smartSearchPool;
    private sealed record PinyinSearchRequest(
        IReadOnlyList<ClipboardItem> Primary, IReadOnlyList<ClipboardItem> Pool, string Keyword);

    private async Task<IReadOnlyList<ClipboardItem>> GetSmartSearchPoolAsync(
        long revision, ClipboardContentFilter filter, CancellationToken cancellationToken)
    {
        if (_smartSearchPool is { } cached && cached.Revision == revision && cached.Filter == filter)
        {
            return cached.Items;
        }

        var pool = await _clipboardService.SearchAsync("", SmartSearchScanLimit, 0, filter, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        _smartSearchPool = new SmartSearchPool(revision, filter, pool);
        return pool;
    }

    private static (IReadOnlyList<ClipboardItem> Items, string Status) AppendPinyinMatches(
        PinyinSearchRequest request, CancellationToken cancellationToken)
    {
        var knownIds = request.Primary.Select(item => item.Id).ToHashSet();
        var matches = new List<ClipboardItem>();
        foreach (var item in request.Pool)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!knownIds.Contains(item.Id) && MatchesPinyinInitials(item, request.Keyword))
            {
                knownIds.Add(item.Id);
                matches.Add(item);
            }
        }

        return (request.Primary.Concat(matches).ToArray(),
            matches.Count > 0 ? $"含 {matches.Count} 条拼音首字母匹配" : "");
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

    private static (IReadOnlyList<ClipboardItem> Results, string Status) FilterByRegex(
        IReadOnlyList<ClipboardItem> pool, string pattern, CancellationToken cancellationToken)
    {
        const int regexTimeoutMilliseconds = 250;
        Regex regex;
        try
        {
            regex = new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
                TimeSpan.FromMilliseconds(regexTimeoutMilliseconds));
        }
        catch (ArgumentException)
        {
            return ([], "正则表达式无效，请检查 re: 后面的内容");
        }

        var matches = new List<ClipboardItem>();
        foreach (var item in pool)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var text = item.TextContent ?? item.PreviewText ?? item.OcrText;
            if (string.IsNullOrEmpty(text)) continue;
            try
            {
                if (regex.IsMatch(text)) matches.Add(item);
            }
            catch (RegexMatchTimeoutException)
            {
                return ([], "正则匹配超时，请简化表达式后重试");
            }
        }

        return (matches, $"正则匹配 {matches.Count} 条");
    }

    private static bool ShouldAugmentWithPinyin(string keyword, int primaryCount)
    {
        const int minimumInitialsLength = 2;
        const int maximumInitialsLength = 12;
        var trimmed = keyword.Trim();
        return trimmed.Length >= minimumInitialsLength && trimmed.Length <= maximumInitialsLength &&
            primaryCount < PageSize && trimmed.All(char.IsAsciiLetter);
    }

    private static bool MatchesPinyinInitials(ClipboardItem item, string keyword)
    {
        return PinyinInitialMatcher.Matches(item.PreviewText, keyword) ||
            PinyinInitialMatcher.Matches(item.TextContent, keyword) ||
            PinyinInitialMatcher.Matches(item.OcrText, keyword);
    }
}
