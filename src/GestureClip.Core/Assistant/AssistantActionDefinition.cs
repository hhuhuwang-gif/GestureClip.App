namespace GestureClip.Core.Assistant;

public sealed record AssistantActionDefinition(
    string Id,
    string DisplayName,
    string Category,
    string Description,
    AssistantInputKind RequiredInput,
    AssistantOutputKind DefaultOutput,
    AssistantPrivacyLevel PrivacyLevel);
