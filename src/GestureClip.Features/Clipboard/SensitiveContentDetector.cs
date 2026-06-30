using System.Text.RegularExpressions;
using GestureClip.Core.Abstractions;

namespace GestureClip.Features.Clipboard;

public sealed partial class SensitiveContentDetector : ISensitiveContentDetector
{
    public bool LooksSensitive(string text)
    {
        var trimmed = text.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return false;
        }

        if (VerificationCodeRegex().IsMatch(trimmed))
        {
            return true;
        }

        if (trimmed.Contains("sk-", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Contains("AKIA", StringComparison.Ordinal) ||
            trimmed.Contains("ghp_", StringComparison.Ordinal) ||
            trimmed.Contains("xoxb-", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return BankCardLikeRegex().IsMatch(trimmed) || JwtLikeRegex().IsMatch(trimmed);
    }

    [GeneratedRegex(@"^\d{4,8}$")]
    private static partial Regex VerificationCodeRegex();

    [GeneratedRegex(@"\b\d{13,19}\b")]
    private static partial Regex BankCardLikeRegex();

    [GeneratedRegex(@"^[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+$")]
    private static partial Regex JwtLikeRegex();
}
