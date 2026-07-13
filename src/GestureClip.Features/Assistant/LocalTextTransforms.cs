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

    [GeneratedRegex(@"[ \t\f\v]+")]
    private static partial Regex MultiWhitespaceRegex();

    [GeneratedRegex(@"\n{3,}")]
    private static partial Regex MultiBlankLinesRegex();
}
