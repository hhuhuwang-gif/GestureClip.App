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

    // Do not blanket-classify every 4-8 digit clipboard value as an OTP. Years, prices,
    // document ids and table cells are common clipboard content. Require nearby OTP context;
    // password/login windows are still protected by ISensitiveCaptureGate.
    [GeneratedRegex(
        @"(?ix)(?:(?:验证码|校验码|动态码|一次性密码|otp|verification\s*code|security\s*code|passcode)\D{0,16}\d{4,8}\b|\b\d{4,8}\D{0,24}(?:验证码|校验码|otp|verification\s*code|security\s*code|passcode))")]
    private static partial Regex VerificationCodeRegex();

    [GeneratedRegex(@"\b\d{13,19}\b")]
    private static partial Regex BankCardLikeRegex();

    [GeneratedRegex(@"^[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+$")]
    private static partial Regex JwtLikeRegex();
}
