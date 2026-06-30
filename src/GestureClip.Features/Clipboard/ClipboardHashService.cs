using System.Security.Cryptography;
using System.Text;
using GestureClip.Core.Abstractions;

namespace GestureClip.Features.Clipboard;

public sealed class ClipboardHashService : IClipboardHashService
{
    public string ComputeHash(string text)
    {
        return ComputeSha256(text);
    }

    public string ComputePlainTextHash(string text)
    {
        var normalized = text.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace("\r", "\n", StringComparison.Ordinal)
            .Trim();

        return ComputeSha256(normalized);
    }

    private static string ComputeSha256(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
