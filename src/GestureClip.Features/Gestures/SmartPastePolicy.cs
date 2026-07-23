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
    private static readonly HashSet<string> ChatProcesses = new(StringComparer.OrdinalIgnoreCase)
    {
        "WeChat.exe",
        "Weixin.exe",
        "WXWork.exe",
        "WeComApp.exe",
        "Feishu.exe",
        "Lark.exe",
        "Teams.exe",
        "ms-teams.exe",
        "Slack.exe",
        "QQ.exe",
        "TIM.exe",
        "DingTalk.exe",
        "Discord.exe",
        "Telegram.exe",
        "WhatsApp.exe",
        "Line.exe"
    };

    private static readonly HashSet<string> BrowserProcesses = new(StringComparer.OrdinalIgnoreCase)
    {
        "chrome.exe",
        "msedge.exe",
        "firefox.exe",
        "brave.exe",
        "opera.exe",
        "arc.exe"
    };

    private static readonly HashSet<string> OfficeProcesses = new(StringComparer.OrdinalIgnoreCase)
    {
        "WINWORD.EXE",
        "EXCEL.EXE",
        "POWERPNT.EXE"
    };

    private static readonly HashSet<string> CodeProcesses = new(StringComparer.OrdinalIgnoreCase)
    {
        "Code.exe",
        "devenv.exe",
        "Cursor.exe",
        "rider64.exe",
        "idea64.exe",
        "pycharm64.exe",
        "webstorm64.exe",
        "clion64.exe",
        "datagrip64.exe",
        "phpstorm64.exe",
        "goland64.exe",
        "WindowsTerminal.exe",
        "wt.exe",
        "powershell.exe",
        "pwsh.exe",
        "cmd.exe"
    };

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

        var processName = NormalizeProcessName(app.ProcessName);
        var windowTitle = app.WindowTitle ?? "";

        if (ChatProcesses.Contains(processName))
        {
            return SmartPasteStrategy.PlainTextPaste;
        }

        if (OfficeProcesses.Contains(processName))
        {
            return SmartPasteStrategy.NormalPaste;
        }

        if (CodeProcesses.Contains(processName))
        {
            return SmartPasteStrategy.PlainTextPaste;
        }

        if (BrowserProcesses.Contains(processName) ||
            windowTitle.Contains("ChatGPT", StringComparison.OrdinalIgnoreCase))
        {
            return SmartPasteStrategy.CleanTextPaste;
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

    private static string NormalizeProcessName(string? processName)
    {
        if (string.IsNullOrWhiteSpace(processName))
        {
            return "";
        }

        var normalized = processName.Trim();
        return normalized.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? normalized
            : $"{normalized}.exe";
    }
}
