using GestureClip.Core.SystemInfo;
using GestureClip.Features.Assistant;

namespace GestureClip.Features.Gestures;

public enum SmartPasteStrategy
{
    NormalPaste,
    PlainTextPaste,
    CleanTextPaste
}

public static class SmartPastePolicy
{
    /// <summary>Prepare clipboard text for the current foreground app strategy.</summary>
    public static string TransformForStrategy(string text, SmartPasteStrategy strategy)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        return strategy switch
        {
            // SetTextAsync already writes Unicode-only; strip HTML if present, keep code/newlines intact.
            SmartPasteStrategy.PlainTextPaste => LocalTextTransforms.ToPlainText(text),
            SmartPasteStrategy.CleanTextPaste => CleanText(LocalTextTransforms.ToPlainText(text)),
            _ => text
        };
    }

    public static SmartPasteStrategy Select(ForegroundAppInfo app, string? strategyOverride = null)
    {
        if (TryParseStrategy(strategyOverride, out var overrideStrategy))
        {
            return overrideStrategy;
        }

        return SmartPasteStrategy.NormalPaste;
    }

    public static string CleanText(string text)
    {
        var normalized = text
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace("\r", "\n", StringComparison.Ordinal);
        var lines = normalized.Split('\n');
        var result = new List<string>(lines.Length);
        var blankCount = 0;

        foreach (var line in lines)
        {
            var cleanedLine = line.TrimEnd();
            if (cleanedLine.Length == 0)
            {
                blankCount++;
                if (blankCount <= 1)
                {
                    result.Add("");
                }

                continue;
            }

            blankCount = 0;
            result.Add(cleanedLine);
        }

        while (result.Count > 0 && result[0].Length == 0)
        {
            result.RemoveAt(0);
        }

        while (result.Count > 0 && result[^1].Length == 0)
        {
            result.RemoveAt(result.Count - 1);
        }

        return string.Join("\r\n", result);
    }

    public static bool TryParseStrategy(string? strategy, out SmartPasteStrategy result)
    {
        result = SmartPasteStrategy.NormalPaste;
        if (string.IsNullOrWhiteSpace(strategy))
        {
            return false;
        }

        switch (strategy.Trim().ToLowerInvariant())
        {
            case "plain":
            case "plaintext":
            case "plaintextpaste":
                result = SmartPasteStrategy.PlainTextPaste;
                return true;
            case "clean":
            case "cleantext":
            case "cleantextpaste":
                result = SmartPasteStrategy.CleanTextPaste;
                return true;
            case "normal":
            case "normalpaste":
                result = SmartPasteStrategy.NormalPaste;
                return true;
            default:
                return Enum.TryParse(strategy, ignoreCase: true, out result);
        }
    }

}
