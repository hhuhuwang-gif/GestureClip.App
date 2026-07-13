using GestureClip.Core.Gestures;

namespace GestureClip.Features.Assistant;

public static class GestureAssistantActionMap
{
    public static string? ToAssistantActionId(BuiltInGestureAction action) => action switch
    {
        BuiltInGestureAction.AssistantTrim => BuiltInAssistantActionCatalog.TrimId,
        BuiltInGestureAction.AssistantNormalizeWhitespace => BuiltInAssistantActionCatalog.NormalizeWhitespaceId,
        BuiltInGestureAction.AssistantCollapseBlankLines => BuiltInAssistantActionCatalog.CollapseBlankLinesId,
        BuiltInGestureAction.AssistantUpper => BuiltInAssistantActionCatalog.UpperId,
        BuiltInGestureAction.AssistantLower => BuiltInAssistantActionCatalog.LowerId,
        BuiltInGestureAction.AssistantTitleCase => BuiltInAssistantActionCatalog.TitleCaseId,
        BuiltInGestureAction.AssistantJsonFormat => BuiltInAssistantActionCatalog.JsonFormatId,
        BuiltInGestureAction.AssistantJsonMinify => BuiltInAssistantActionCatalog.JsonMinifyId,
        BuiltInGestureAction.AssistantUrlEncode => BuiltInAssistantActionCatalog.UrlEncodeId,
        BuiltInGestureAction.AssistantUrlDecode => BuiltInAssistantActionCatalog.UrlDecodeId,
        BuiltInGestureAction.AssistantQuote => BuiltInAssistantActionCatalog.QuoteId,
        BuiltInGestureAction.AssistantUnquote => BuiltInAssistantActionCatalog.UnquoteId,
        _ => null
    };

    public static bool IsAssistantTextAction(BuiltInGestureAction action) =>
        ToAssistantActionId(action) is not null;
}
