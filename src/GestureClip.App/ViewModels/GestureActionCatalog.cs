using GestureClip.Core.Gestures;

namespace GestureClip.App.ViewModels;

public static class GestureActionCatalog
{
    public const string CommonCategory = "常用动作";
    public const string AssistantCategory = "快捷动作 / 文本处理";
    public const string WebSearchCategory = "网页搜索";
    public const string MoreCategory = "更多动作";

    public static IReadOnlyList<GestureActionOptionViewModel> DefaultOptions { get; } =
    [
        Option(BuiltInGestureAction.None),
        Option(BuiltInGestureAction.Copy),
        Option(BuiltInGestureAction.Paste),
        Option(BuiltInGestureAction.SmartPaste),
        Option(BuiltInGestureAction.PastePlainText),
        Option(BuiltInGestureAction.Cut),
        Option(BuiltInGestureAction.SelectAll),
        Option(BuiltInGestureAction.Undo),
        Option(BuiltInGestureAction.Redo),
        Option(BuiltInGestureAction.Enter),
        Option(BuiltInGestureAction.Escape),
        Option(BuiltInGestureAction.Backspace),
        Option(BuiltInGestureAction.Delete),
        Option(BuiltInGestureAction.SendAltLeft),
        Option(BuiltInGestureAction.SendAltRight),
        Option(BuiltInGestureAction.OpenClipboardOverlay),
        Option(BuiltInGestureAction.OpenQuickActionCenter),
        Option(BuiltInGestureAction.PasteAndEnter),
        Option(BuiltInGestureAction.AssistantTrim),
        Option(BuiltInGestureAction.AssistantNormalizeWhitespace),
        Option(BuiltInGestureAction.AssistantCollapseBlankLines),
        Option(BuiltInGestureAction.AssistantUpper),
        Option(BuiltInGestureAction.AssistantLower),
        Option(BuiltInGestureAction.AssistantTitleCase),
        Option(BuiltInGestureAction.AssistantJsonFormat),
        Option(BuiltInGestureAction.AssistantJsonMinify),
        Option(BuiltInGestureAction.AssistantUrlEncode),
        Option(BuiltInGestureAction.AssistantUrlDecode),
        Option(BuiltInGestureAction.AssistantQuote),
        Option(BuiltInGestureAction.AssistantUnquote),
        Option(BuiltInGestureAction.AssistantPlainText),
        Option(BuiltInGestureAction.AssistantHtmlToText),
        Option(BuiltInGestureAction.AssistantToMarkdown),
        Option(BuiltInGestureAction.AssistantCleanUrl),
        Option(BuiltInGestureAction.SearchSelectedTextWithGoogle),
        Option(BuiltInGestureAction.SearchSelectedTextWithBaidu),
        Option(BuiltInGestureAction.SearchSelectedTextWithBing),
        Option(BuiltInGestureAction.OpenGoogle),
        Option(BuiltInGestureAction.OpenBaidu),
        Option(BuiltInGestureAction.PasteLatestClipboardItem),
        Option(BuiltInGestureAction.NewTab),
        Option(BuiltInGestureAction.NextTab),
        Option(BuiltInGestureAction.PreviousTab),
        Option(BuiltInGestureAction.ReopenClosedTab),
        Option(BuiltInGestureAction.Refresh),
        Option(BuiltInGestureAction.CloseTab),
        Option(BuiltInGestureAction.StartMenu),
        Option(BuiltInGestureAction.ShowDesktop),
        Option(BuiltInGestureAction.SwitchApp),
        Option(BuiltInGestureAction.TaskSwitcher),
        Option(BuiltInGestureAction.PlayPause),
        Option(BuiltInGestureAction.VolumeUp),
        Option(BuiltInGestureAction.VolumeDown),
        Option(BuiltInGestureAction.Mute),
        Option(BuiltInGestureAction.PreviousTrack),
        Option(BuiltInGestureAction.NextTrack),
        Option(BuiltInGestureAction.TaskManager),
        Option(BuiltInGestureAction.SystemSettings),
        Option(BuiltInGestureAction.Sleep),
        Option(BuiltInGestureAction.ZoomIn),
        Option(BuiltInGestureAction.ZoomOut),
        Option(BuiltInGestureAction.ResetZoom),
        Option(BuiltInGestureAction.Home),
        Option(BuiltInGestureAction.End),
        Option(BuiltInGestureAction.PageUp),
        Option(BuiltInGestureAction.PageDown),
        Option(BuiltInGestureAction.Screenshot),
        Option(BuiltInGestureAction.NextVirtualDesktop),
        Option(BuiltInGestureAction.PreviousVirtualDesktop),
        Option(BuiltInGestureAction.FullScreen),
        Option(BuiltInGestureAction.PinWindow)
    ];

    public static GestureActionOptionViewModel Option(BuiltInGestureAction action)
    {
        return new GestureActionOptionViewModel(
            action,
            GestureActionText.Name(action),
            GestureActionText.Shortcut(action),
            Category(action),
            Description(action));
    }

    public static string Description(BuiltInGestureAction action) => action switch
    {
        BuiltInGestureAction.SmartPaste => "根据当前软件自动选择普通粘贴、纯文本粘贴或干净粘贴。",
        BuiltInGestureAction.PastePlainText => "强制纯文本粘贴（等同全局 Ctrl+Shift+V）。",
        BuiltInGestureAction.OpenQuickActionCenter => "打开本地快捷动作面板，可搜索并执行文本处理。",
        BuiltInGestureAction.AssistantTrim => "去掉剪贴板文本首尾空白，并写回系统剪贴板。",
        BuiltInGestureAction.AssistantNormalizeWhitespace => "合并多余空格后写回剪贴板。",
        BuiltInGestureAction.AssistantCollapseBlankLines => "合并多余空行后写回剪贴板。",
        BuiltInGestureAction.AssistantUpper => "转成大写后写回剪贴板。",
        BuiltInGestureAction.AssistantLower => "转成小写后写回剪贴板。",
        BuiltInGestureAction.AssistantTitleCase => "转成标题大小写后写回剪贴板。",
        BuiltInGestureAction.AssistantJsonFormat => "美化 JSON 后写回剪贴板。",
        BuiltInGestureAction.AssistantJsonMinify => "压缩 JSON 后写回剪贴板。",
        BuiltInGestureAction.AssistantUrlEncode => "URL 编码后写回剪贴板。",
        BuiltInGestureAction.AssistantUrlDecode => "URL 解码后写回剪贴板。",
        BuiltInGestureAction.AssistantQuote => "给文本加双引号后写回剪贴板。",
        BuiltInGestureAction.AssistantUnquote => "去掉外层引号后写回剪贴板。",
        BuiltInGestureAction.AssistantPlainText => "转为纯文本后写回剪贴板。",
        BuiltInGestureAction.AssistantHtmlToText => "HTML 转纯文本后写回剪贴板。",
        BuiltInGestureAction.AssistantToMarkdown => "转为轻量 Markdown 后写回剪贴板。",
        BuiltInGestureAction.AssistantCleanUrl => "去掉链接追踪参数后写回剪贴板。",
        _ => ""
    };

    public static string Category(BuiltInGestureAction action) => action switch
    {
        BuiltInGestureAction.OpenQuickActionCenter or
        BuiltInGestureAction.AssistantTrim or
        BuiltInGestureAction.AssistantNormalizeWhitespace or
        BuiltInGestureAction.AssistantCollapseBlankLines or
        BuiltInGestureAction.AssistantUpper or
        BuiltInGestureAction.AssistantLower or
        BuiltInGestureAction.AssistantTitleCase or
        BuiltInGestureAction.AssistantJsonFormat or
        BuiltInGestureAction.AssistantJsonMinify or
        BuiltInGestureAction.AssistantUrlEncode or
        BuiltInGestureAction.AssistantUrlDecode or
        BuiltInGestureAction.AssistantQuote or
        BuiltInGestureAction.AssistantUnquote or
        BuiltInGestureAction.AssistantPlainText or
        BuiltInGestureAction.AssistantHtmlToText or
        BuiltInGestureAction.AssistantToMarkdown or
        BuiltInGestureAction.AssistantCleanUrl => AssistantCategory,

        BuiltInGestureAction.SearchSelectedTextWithGoogle or
        BuiltInGestureAction.SearchSelectedTextWithBaidu or
        BuiltInGestureAction.SearchSelectedTextWithBing or
        BuiltInGestureAction.OpenGoogle or
        BuiltInGestureAction.OpenBaidu => WebSearchCategory,

        BuiltInGestureAction.NewTab or
        BuiltInGestureAction.NextTab or
        BuiltInGestureAction.PreviousTab or
        BuiltInGestureAction.ReopenClosedTab or
        BuiltInGestureAction.Refresh or
        BuiltInGestureAction.CloseTab or
        BuiltInGestureAction.StartMenu or
        BuiltInGestureAction.ShowDesktop or
        BuiltInGestureAction.SwitchApp or
        BuiltInGestureAction.TaskSwitcher or
        BuiltInGestureAction.PlayPause or
        BuiltInGestureAction.VolumeUp or
        BuiltInGestureAction.VolumeDown or
        BuiltInGestureAction.Mute or
        BuiltInGestureAction.PreviousTrack or
        BuiltInGestureAction.NextTrack or
        BuiltInGestureAction.TaskManager or
        BuiltInGestureAction.SystemSettings or
        BuiltInGestureAction.Sleep or
        BuiltInGestureAction.ZoomIn or
        BuiltInGestureAction.ZoomOut or
        BuiltInGestureAction.ResetZoom or
        BuiltInGestureAction.Home or
        BuiltInGestureAction.End or
        BuiltInGestureAction.PageUp or
        BuiltInGestureAction.PageDown or
        BuiltInGestureAction.Screenshot or
        BuiltInGestureAction.NextVirtualDesktop or
        BuiltInGestureAction.PreviousVirtualDesktop or
        BuiltInGestureAction.FullScreen or
        BuiltInGestureAction.PinWindow => MoreCategory,

        _ => CommonCategory
    };
}
