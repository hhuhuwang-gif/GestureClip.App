using GestureClip.Core.Settings;
using Microsoft.Extensions.Logging;

namespace GestureClip.Features.Clipboard;

public sealed partial class ClipboardService
{
    private void MarkHistoryChanged()
    {
        Interlocked.Increment(ref _historyRevision);
    }

    private bool IsPerfLogEnabled()
    {
        return _perfLogEnabled;
    }

    [System.Diagnostics.Conditional("DEBUG")]
    private static void DebugPerf(string eventName, string detail = "")
    {
        var suffix = detail.Length == 0 ? string.Empty : $" {detail}";
        System.Diagnostics.Trace.WriteLine($"ClipboardPerf {eventName}{suffix}");
    }

    private void LogPerf(string eventName, long elapsedMs, params (string Key, object? Value)[] values)
    {
        if (!IsPerfLogEnabled())
        {
            return;
        }

        var details = values.Length == 0
            ? string.Empty
            : " " + string.Join(" ", values.Select(value => $"{value.Key}={value.Value}"));
        _logger.LogInformation("ClipboardPerf {EventName} ElapsedMs={ElapsedMs}{Details}", eventName, elapsedMs, details);
    }

    private int GetMaxImageBytes()
    {
        var configured = _settingsService.Get(SettingKeys.ClipboardMaxImageBytes, DefaultMaxImageBytes);
        if (configured <= 0)
        {
            return DefaultMaxImageBytes;
        }

        return Math.Clamp(configured, MinMaxImageBytes, MaxMaxImageBytes);
    }

    private static long EstimateBase64DecodedByteCount(string value)
    {
        var base64 = NormalizeBase64(value);
        if (base64.Length == 0)
        {
            return 0;
        }

        var padding = 0;
        if (base64.EndsWith("==", StringComparison.Ordinal))
        {
            padding = 2;
        }
        else if (base64.EndsWith("=", StringComparison.Ordinal))
        {
            padding = 1;
        }

        return (base64.Length * 3L / 4L) - padding;
    }

    private static string NormalizeBase64(string value)
    {
        var trimmed = value.Trim();
        var commaIndex = trimmed.IndexOf(',', StringComparison.Ordinal);
        if (trimmed.StartsWith("data:", StringComparison.OrdinalIgnoreCase) && commaIndex >= 0)
        {
            trimmed = trimmed[(commaIndex + 1)..];
        }

        return new string(trimmed.Where(character => !char.IsWhiteSpace(character)).ToArray());
    }

    private static string CreatePreview(string text)
    {
        var compact = text.Replace("\r\n", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal)
            .Replace("\r", " ", StringComparison.Ordinal)
            .Trim();

        return compact.Length <= 200 ? compact : compact[..200];
    }
}
