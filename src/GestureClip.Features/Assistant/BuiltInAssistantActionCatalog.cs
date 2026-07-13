using GestureClip.Core.Abstractions;
using GestureClip.Core.Assistant;

namespace GestureClip.Features.Assistant;

public sealed class BuiltInAssistantActionCatalog : IAssistantActionCatalog
{
    public const string TrimId = "text.trim";
    public const string NormalizeWhitespaceId = "text.normalize_whitespace";
    public const string UpperId = "text.upper";
    public const string LowerId = "text.lower";
    public const string TitleCaseId = "text.title_case";
    public const string JsonFormatId = "text.json_format";
    public const string JsonMinifyId = "text.json_minify";
    public const string UrlEncodeId = "text.url_encode";
    public const string UrlDecodeId = "text.url_decode";
    public const string QuoteId = "text.quote";
    public const string UnquoteId = "text.unquote";
    public const string CollapseBlankLinesId = "text.collapse_blank_lines";
    public const string PlainTextId = "text.plain";
    public const string HtmlToTextId = "text.html_to_text";
    public const string ToMarkdownId = "text.to_markdown";
    public const string CleanUrlId = "text.clean_url";
    public const string OpenSettingsId = "app.open_settings";
    public const string ExportDiagnosticsId = "app.export_diagnostics";

    private static readonly IReadOnlyList<AssistantActionDefinition> Actions =
    [
        new(TrimId, "去除首尾空格", "文本整理", "去掉剪贴板文本开头和结尾的空白。", AssistantInputKind.ClipboardText, AssistantOutputKind.Clipboard, AssistantPrivacyLevel.LocalOnly),
        new(NormalizeWhitespaceId, "合并多余空格", "文本整理", "把连续空白压成单个空格，并去掉首尾空白。", AssistantInputKind.ClipboardText, AssistantOutputKind.Clipboard, AssistantPrivacyLevel.LocalOnly),
        new(CollapseBlankLinesId, "合并多余空行", "文本整理", "把连续空行压成最多一个空行。", AssistantInputKind.ClipboardText, AssistantOutputKind.Clipboard, AssistantPrivacyLevel.LocalOnly),
        new(PlainTextId, "转为纯文本", "粘贴变形", "去掉 HTML/多余空白，只保留可读纯文本。", AssistantInputKind.ClipboardText, AssistantOutputKind.Clipboard, AssistantPrivacyLevel.LocalOnly),
        new(HtmlToTextId, "HTML 转文本", "粘贴变形", "剥离 HTML 标签与脚本样式，保留正文。", AssistantInputKind.ClipboardText, AssistantOutputKind.Clipboard, AssistantPrivacyLevel.LocalOnly),
        new(ToMarkdownId, "转为 Markdown", "粘贴变形", "把简单 HTML 转成轻量 Markdown（标题/链接/列表）。", AssistantInputKind.ClipboardText, AssistantOutputKind.Clipboard, AssistantPrivacyLevel.LocalOnly),
        new(CleanUrlId, "链接净化", "粘贴变形", "去掉 utm、spm、fbclid 等追踪参数，保留干净链接。", AssistantInputKind.ClipboardText, AssistantOutputKind.Clipboard, AssistantPrivacyLevel.LocalOnly),
        new(UpperId, "转成大写", "大小写", "把文本全部转成大写。", AssistantInputKind.ClipboardText, AssistantOutputKind.Clipboard, AssistantPrivacyLevel.LocalOnly),
        new(LowerId, "转成小写", "大小写", "把文本全部转成小写。", AssistantInputKind.ClipboardText, AssistantOutputKind.Clipboard, AssistantPrivacyLevel.LocalOnly),
        new(TitleCaseId, "转成标题大小写", "大小写", "按当前语言规则转成标题大小写。", AssistantInputKind.ClipboardText, AssistantOutputKind.Clipboard, AssistantPrivacyLevel.LocalOnly),
        new(JsonFormatId, "JSON 美化", "JSON", "把 JSON 格式化成缩进文本。无效 JSON 会提示失败。", AssistantInputKind.ClipboardText, AssistantOutputKind.Clipboard, AssistantPrivacyLevel.LocalOnly),
        new(JsonMinifyId, "JSON 压缩", "JSON", "把 JSON 压成单行。无效 JSON 会提示失败。", AssistantInputKind.ClipboardText, AssistantOutputKind.Clipboard, AssistantPrivacyLevel.LocalOnly),
        new(UrlEncodeId, "URL 编码", "编码", "对文本做 URL 编码。", AssistantInputKind.ClipboardText, AssistantOutputKind.Clipboard, AssistantPrivacyLevel.LocalOnly),
        new(UrlDecodeId, "URL 解码", "编码", "对文本做 URL 解码。", AssistantInputKind.ClipboardText, AssistantOutputKind.Clipboard, AssistantPrivacyLevel.LocalOnly),
        new(QuoteId, "加双引号", "引号", "给文本加上双引号，并转义内部引号。", AssistantInputKind.ClipboardText, AssistantOutputKind.Clipboard, AssistantPrivacyLevel.LocalOnly),
        new(UnquoteId, "去掉外层引号", "引号", "去掉文本外层的单引号或双引号。", AssistantInputKind.ClipboardText, AssistantOutputKind.Clipboard, AssistantPrivacyLevel.LocalOnly),
        new(OpenSettingsId, "打开设置", "应用", "打开 GestureClip 设置窗口。", AssistantInputKind.None, AssistantOutputKind.None, AssistantPrivacyLevel.LocalOnly),
        new(ExportDiagnosticsId, "导出诊断包", "应用", "导出隐私安全的诊断包，不包含剪贴板正文。", AssistantInputKind.None, AssistantOutputKind.None, AssistantPrivacyLevel.LocalOnly)
    ];

    private static readonly Dictionary<string, AssistantActionDefinition> ById =
        Actions.ToDictionary(x => x.Id, StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<AssistantActionDefinition> GetActions() => Actions;

    public AssistantActionDefinition? GetById(string actionId)
    {
        if (string.IsNullOrWhiteSpace(actionId))
        {
            return null;
        }

        return ById.TryGetValue(actionId.Trim(), out var definition) ? definition : null;
    }
}
