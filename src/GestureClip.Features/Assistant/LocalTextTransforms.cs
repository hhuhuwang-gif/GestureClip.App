using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace GestureClip.Features.Assistant;

public static partial class LocalTextTransforms
{
    private static readonly JsonSerializerOptions PrettyJsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static readonly JsonSerializerOptions CompactJsonOptions = new()
    {
        WriteIndented = false,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static string Trim(string input) => input.Trim();

    public static string NormalizeWhitespace(string input)
    {
        var normalized = MultiWhitespaceRegex().Replace(input.Replace("\r\n", "\n").Replace('\r', '\n'), " ");
        return normalized.Trim();
    }

    public static string ToUpper(string input) => input.ToUpperInvariant();

    public static string ToLower(string input) => input.ToLowerInvariant();

    public static string ToTitleCase(string input)
    {
        var lower = input.ToLower(CultureInfo.CurrentCulture);
        return CultureInfo.CurrentCulture.TextInfo.ToTitleCase(lower);
    }

    public static bool TryFormatJson(string input, out string output, out string? errorClass)
    {
        try
        {
            using var document = JsonDocument.Parse(input);
            output = JsonSerializer.Serialize(document.RootElement, PrettyJsonOptions);
            errorClass = null;
            return true;
        }
        catch (JsonException)
        {
            output = input;
            errorClass = "invalid_json";
            return false;
        }
    }

    public static bool TryMinifyJson(string input, out string output, out string? errorClass)
    {
        try
        {
            using var document = JsonDocument.Parse(input);
            output = JsonSerializer.Serialize(document.RootElement, CompactJsonOptions);
            errorClass = null;
            return true;
        }
        catch (JsonException)
        {
            output = input;
            errorClass = "invalid_json";
            return false;
        }
    }

    public static string UrlEncode(string input) => Uri.EscapeDataString(input);

    public static string UrlDecode(string input)
    {
        try
        {
            return Uri.UnescapeDataString(input.Replace("+", "%20"));
        }
        catch (UriFormatException)
        {
            return input;
        }
    }

    public static string Quote(string input)
    {
        var escaped = input.Replace("\"", "\\\"", StringComparison.Ordinal);
        return $"\"{escaped}\"";
    }

    public static string Unquote(string input)
    {
        var trimmed = input.Trim();
        if (trimmed.Length >= 2)
        {
            var first = trimmed[0];
            var last = trimmed[^1];
            if ((first == '"' && last == '"') || (first == '\'' && last == '\''))
            {
                var inner = trimmed[1..^1];
                if (first == '"')
                {
                    return inner.Replace("\\\"", "\"", StringComparison.Ordinal);
                }

                return inner.Replace("\\'", "'", StringComparison.Ordinal);
            }
        }

        return input;
    }

    public static string CollapseBlankLines(string input)
    {
        var normalized = input.Replace("\r\n", "\n").Replace('\r', '\n');
        return MultiBlankLinesRegex().Replace(normalized, "\n\n").Trim();
    }

    /// <summary>Strip simple HTML tags / entities into readable plain text.</summary>
    public static string HtmlToPlainText(string input)
    {
        if (string.IsNullOrWhiteSpace(input) || input.IndexOf('<') < 0)
        {
            return CollapseBlankLines(input);
        }

        var text = input;
        text = ScriptStyleRegex().Replace(text, " ");
        text = BlockBreakRegex().Replace(text, "\n");
        text = TagRegex().Replace(text, " ");
        text = System.Net.WebUtility.HtmlDecode(text);
        text = text.Replace("\r\n", "\n").Replace('\r', '\n');
        text = MultiWhitespaceRegex().Replace(text, " ");
        text = MultiBlankLinesRegex().Replace(text, "\n\n");
        return text.Trim();
    }

    /// <summary>Lightweight conversion: HTML → Markdown-ish plain structure, or clean plain text.</summary>
    public static string ToMarkdownLite(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return input;
        }

        if (input.IndexOf('<') < 0)
        {
            return CollapseBlankLines(input.Trim());
        }

        var text = ScriptStyleRegex().Replace(input, " ");
        text = HeadingRegex().Replace(text, m =>
        {
            var level = int.TryParse(m.Groups[1].Value, out var n) ? n : 1;
            var body = TagRegex().Replace(m.Groups[2].Value, "").Trim();
            body = System.Net.WebUtility.HtmlDecode(body);
            return "\n" + new string('#', Math.Clamp(level, 1, 6)) + " " + body + "\n";
        });
        text = LinkRegex().Replace(text, m =>
        {
            var href = m.Groups[1].Value.Trim();
            var label = TagRegex().Replace(m.Groups[2].Value, "").Trim();
            label = System.Net.WebUtility.HtmlDecode(label);
            return string.IsNullOrWhiteSpace(label) ? href : $"[{label}]({href})";
        });
        text = BoldRegex().Replace(text, m => $"**{System.Net.WebUtility.HtmlDecode(TagRegex().Replace(m.Groups[2].Value, "").Trim())}**");
        text = ItalicRegex().Replace(text, m => $"*{System.Net.WebUtility.HtmlDecode(TagRegex().Replace(m.Groups[2].Value, "").Trim())}*");
        text = ListItemRegex().Replace(text, m =>
            "\n- " + System.Net.WebUtility.HtmlDecode(TagRegex().Replace(m.Groups[1].Value, "").Trim()));
        text = BlockBreakRegex().Replace(text, "\n");
        text = TagRegex().Replace(text, " ");
        text = System.Net.WebUtility.HtmlDecode(text);
        text = text.Replace("\r\n", "\n").Replace('\r', '\n');
        text = MultiWhitespaceOnLineRegex().Replace(text, " ");
        text = MultiBlankLinesRegex().Replace(text, "\n\n");
        return text.Trim();
    }

    /// <summary>Remove common tracking query parameters from http(s) URLs in text.</summary>
    public static string CleanTrackingUrls(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return input;
        }

        return UrlInTextRegex().Replace(input, match => CleanSingleUrl(match.Value));
    }

    public static string ToPlainText(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return input;
        }

        // Already plain: keep content intact (code/newlines/indent).
        if (input.IndexOf('<') < 0)
        {
            return input;
        }

        return CollapseBlankLines(HtmlToPlainText(input));
    }

    private static string CleanSingleUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return url;
        }

        var builder = new UriBuilder(uri);
        if (string.IsNullOrEmpty(builder.Query))
        {
            return url;
        }

        var query = builder.Query.TrimStart('?');
        if (string.IsNullOrEmpty(query))
        {
            return url;
        }

        var kept = new List<string>();
        foreach (var part in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var eq = part.IndexOf('=');
            var key = eq >= 0 ? part[..eq] : part;
            if (IsTrackingQueryKey(key))
            {
                continue;
            }

            kept.Add(part);
        }

        builder.Query = kept.Count == 0 ? "" : string.Join("&", kept);
        // UriBuilder may add trailing slash differences; prefer original host/path.
        var cleaned = builder.Uri.GetComponents(
            UriComponents.SchemeAndServer | UriComponents.Path | UriComponents.Query,
            UriFormat.UriEscaped);
        if (cleaned.EndsWith('?'))
        {
            cleaned = cleaned[..^1];
        }

        return cleaned;
    }

    private static bool IsTrackingQueryKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return false;
        }

        var k = key.Trim();
        if (k.StartsWith("utm_", StringComparison.OrdinalIgnoreCase) ||
            k.StartsWith("spm", StringComparison.OrdinalIgnoreCase) ||
            k.StartsWith("scm", StringComparison.OrdinalIgnoreCase) ||
            k.StartsWith("clk", StringComparison.OrdinalIgnoreCase) ||
            k.StartsWith("gclid", StringComparison.OrdinalIgnoreCase) ||
            k.StartsWith("fbclid", StringComparison.OrdinalIgnoreCase) ||
            k.StartsWith("mc_", StringComparison.OrdinalIgnoreCase) ||
            k.StartsWith("pk_", StringComparison.OrdinalIgnoreCase) ||
            k.StartsWith("mtm_", StringComparison.OrdinalIgnoreCase) ||
            k.StartsWith("vero_", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return k.Equals("spm", StringComparison.OrdinalIgnoreCase) ||
               k.Equals("spm_id_from", StringComparison.OrdinalIgnoreCase) ||
               k.Equals("from_spmid", StringComparison.OrdinalIgnoreCase) ||
               k.Equals("share_source", StringComparison.OrdinalIgnoreCase) ||
               k.Equals("share_medium", StringComparison.OrdinalIgnoreCase) ||
               k.Equals("share_plat", StringComparison.OrdinalIgnoreCase) ||
               k.Equals("share_session_id", StringComparison.OrdinalIgnoreCase) ||
               k.Equals("share_tag", StringComparison.OrdinalIgnoreCase) ||
               k.Equals("share_from", StringComparison.OrdinalIgnoreCase) ||
               k.Equals("tt_from", StringComparison.OrdinalIgnoreCase) ||
               k.Equals("vd_source", StringComparison.OrdinalIgnoreCase) ||
               k.Equals("ref_src", StringComparison.OrdinalIgnoreCase) ||
               k.Equals("ref_url", StringComparison.OrdinalIgnoreCase) ||
               k.Equals("si", StringComparison.OrdinalIgnoreCase) ||
               k.Equals("feature", StringComparison.OrdinalIgnoreCase) ||
               k.Equals("yclid", StringComparison.OrdinalIgnoreCase) ||
               k.Equals("msclkid", StringComparison.OrdinalIgnoreCase) ||
               k.Equals("igshid", StringComparison.OrdinalIgnoreCase) ||
               k.Equals("_hsenc", StringComparison.OrdinalIgnoreCase) ||
               k.Equals("_hsmi", StringComparison.OrdinalIgnoreCase) ||
               k.Equals("mc_cid", StringComparison.OrdinalIgnoreCase) ||
               k.Equals("mc_eid", StringComparison.OrdinalIgnoreCase);
    }

    [GeneratedRegex(@"[ \t\f\v]+")]
    private static partial Regex MultiWhitespaceRegex();

    [GeneratedRegex(@"[ \t\f\v]+")]
    private static partial Regex MultiWhitespaceOnLineRegex();

    [GeneratedRegex(@"\n{3,}")]
    private static partial Regex MultiBlankLinesRegex();

    [GeneratedRegex(@"<(script|style)[\s\S]*?</\1>", RegexOptions.IgnoreCase)]
    private static partial Regex ScriptStyleRegex();

    [GeneratedRegex(@"</?(p|div|br|tr|li|h[1-6]|section|article|header|footer|blockquote)[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex BlockBreakRegex();

    [GeneratedRegex(@"<[^>]+>")]
    private static partial Regex TagRegex();

    [GeneratedRegex(@"<h([1-6])[^>]*>([\s\S]*?)</h\1>", RegexOptions.IgnoreCase)]
    private static partial Regex HeadingRegex();

    [GeneratedRegex(@"<a\s+[^>]*href\s*=\s*[""']([^""']+)[""'][^>]*>([\s\S]*?)</a>", RegexOptions.IgnoreCase)]
    private static partial Regex LinkRegex();

    [GeneratedRegex(@"<(strong|b)[^>]*>([\s\S]*?)</\1>", RegexOptions.IgnoreCase)]
    private static partial Regex BoldRegex();

    [GeneratedRegex(@"<(em|i)[^>]*>([\s\S]*?)</\1>", RegexOptions.IgnoreCase)]
    private static partial Regex ItalicRegex();

    [GeneratedRegex(@"<li[^>]*>([\s\S]*?)</li>", RegexOptions.IgnoreCase)]
    private static partial Regex ListItemRegex();

    [GeneratedRegex(@"https?://[^\s<>""']+", RegexOptions.IgnoreCase)]
    private static partial Regex UrlInTextRegex();
}
