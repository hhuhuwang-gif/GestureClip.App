using System.Collections.Concurrent;
using System.Globalization;
using System.Text;

namespace GestureClip.Features.Clipboard;

/// <summary>
/// Lightweight pinyin-initial matcher: lets an ASCII keyword like "wx" match text
/// containing 「微信」. Initials for CJK characters are resolved with the zh-CN
/// collation boundary trick (no dictionary payload); ASCII letters/digits pass through.
/// Rare characters may resolve imprecisely — this is used only as a supplemental
/// search layer on top of the regular substring search, so misses are non-fatal.
/// </summary>
public static class PinyinInitialMatcher
{
    private const int MaxScanChars = 2048;

    private static readonly CompareInfo ChineseCompare = new CultureInfo("zh-CN").CompareInfo;

    // Boundary characters: the first common character of each pinyin initial group.
    private static readonly (string Boundary, char Initial)[] Boundaries =
    [
        ("啊", 'a'), ("芭", 'b'), ("擦", 'c'), ("搭", 'd'), ("蛾", 'e'), ("发", 'f'),
        ("噶", 'g'), ("哈", 'h'), ("击", 'j'), ("喀", 'k'), ("垃", 'l'), ("妈", 'm'),
        ("拿", 'n'), ("哦", 'o'), ("啪", 'p'), ("期", 'q'), ("然", 'r'), ("撒", 's'),
        ("塌", 't'), ("挖", 'w'), ("昔", 'x'), ("压", 'y'), ("匝", 'z')
    ];

    private static readonly ConcurrentDictionary<char, char> InitialCache = new();

    /// <summary>True when the initial-letter sequence of <paramref name="text"/> contains <paramref name="initials"/>.</summary>
    public static bool Matches(string? text, string? initials)
    {
        if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(initials))
        {
            return false;
        }

        var target = initials.Trim().ToLowerInvariant();
        if (target.Length == 0)
        {
            return false;
        }

        var sequence = BuildInitialSequence(text);
        return sequence.Contains(target, StringComparison.Ordinal);
    }

    /// <summary>Initial sequence: CJK chars → pinyin initial, ASCII letters/digits → themselves (lowercase).</summary>
    public static string BuildInitialSequence(string text)
    {
        var builder = new StringBuilder(Math.Min(text.Length, MaxScanChars));
        var scanned = 0;
        foreach (var character in text)
        {
            if (++scanned > MaxScanChars)
            {
                break;
            }

            if (char.IsAsciiLetterOrDigit(character))
            {
                builder.Append(char.ToLowerInvariant(character));
                continue;
            }

            if (GetInitial(character) is { } initial)
            {
                builder.Append(initial);
            }
        }

        return builder.ToString();
    }

    /// <summary>Pinyin initial for a CJK unified ideograph, or null when unknown.</summary>
    public static char? GetInitial(char character)
    {
        if (character is < '一' or > '鿿')
        {
            return null;
        }

        var cached = InitialCache.GetOrAdd(character, ResolveInitial);
        return cached == '\0' ? null : cached;
    }

    private static char ResolveInitial(char character)
    {
        try
        {
            var text = character.ToString();
            var result = '\0';
            foreach (var (boundary, initial) in Boundaries)
            {
                if (ChineseCompare.Compare(text, boundary) >= 0)
                {
                    result = initial;
                }
                else
                {
                    break;
                }
            }

            return result;
        }
        catch (Exception)
        {
            // Collation data unavailable → treat as unknown rather than crash the search.
            return '\0';
        }
    }
}
