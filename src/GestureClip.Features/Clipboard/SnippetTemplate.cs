using System.Text.RegularExpressions;

namespace GestureClip.Features.Clipboard;

/// <summary>
/// Expands {date}/{time} style variables inside user snippets (favorite items) at paste time.
/// Unknown variables are left untouched so normal braces in code snippets survive.
/// </summary>
public static partial class SnippetTemplate
{
    [GeneratedRegex(@"\{(date|time|datetime|year|month|day|weekday)\}", RegexOptions.IgnoreCase)]
    private static partial Regex VariablePattern();

    private static readonly string[] WeekdayNames = ["周日", "周一", "周二", "周三", "周四", "周五", "周六"];

    public static bool ContainsVariables(string? text)
    {
        return !string.IsNullOrEmpty(text) && VariablePattern().IsMatch(text);
    }

    public static string Expand(string text, DateTimeOffset now)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        var local = now.ToLocalTime();
        return VariablePattern().Replace(text, match => match.Groups[1].Value.ToLowerInvariant() switch
        {
            "date" => local.ToString("yyyy-MM-dd"),
            "time" => local.ToString("HH:mm"),
            "datetime" => local.ToString("yyyy-MM-dd HH:mm"),
            "year" => local.Year.ToString(),
            "month" => local.Month.ToString("00"),
            "day" => local.Day.ToString("00"),
            "weekday" => WeekdayNames[(int)local.DayOfWeek],
            _ => match.Value
        });
    }
}
