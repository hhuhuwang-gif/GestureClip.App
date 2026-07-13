namespace GestureClip.Core.Assistant;

public sealed record AssistantActionRequest(
    string ActionId,
    string? InputText = null,
    AssistantOutputKind? OutputOverride = null);
