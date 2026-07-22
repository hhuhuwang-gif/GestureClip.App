namespace GestureClip.Infrastructure.Clipboard;

/// <summary>
/// Best-effort conversion of Windows CF_HTML payloads into plain text for history capture.
/// </summary>
public static class ClipboardHtmlText
{
    public static string? ExtractPlain(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return null;
        }

        var body = html;
        const string startMark = "<!--StartFragment-->";
        const string endMark = "<!--EndFragment-->";
        var start = body.IndexOf(startMark, StringComparison.OrdinalIgnoreCase);
        var end = body.IndexOf(endMark, StringComparison.OrdinalIgnoreCase);
        if (start >= 0 && end > start)
        {
            body = body[(start + startMark.Length)..end];
        }

        body = System.Text.RegularExpressions.Regex.Replace(
            body,
            @"<(script|style)[\s\S]*?</\1>",
            " ",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        body = System.Text.RegularExpressions.Regex.Replace(
            body,
            @"</?(p|div|br|tr|li|h[1-6]|section|article|header|footer|blockquote)[^>]*>",
            "\n",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        body = System.Text.RegularExpressions.Regex.Replace(body, @"<[^>]+>", " ");
        body = System.Net.WebUtility.HtmlDecode(body);
        body = body.Replace("\r\n", "\n").Replace('\r', '\n');
        body = System.Text.RegularExpressions.Regex.Replace(body, @"[ \t\f\v]+", " ");
        body = System.Text.RegularExpressions.Regex.Replace(body, @"\n{3,}", "\n\n");
        body = body.Trim();
        return string.IsNullOrWhiteSpace(body) ? null : body;
    }
}
