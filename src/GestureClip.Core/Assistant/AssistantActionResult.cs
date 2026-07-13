namespace GestureClip.Core.Assistant;

public sealed record AssistantActionResult(
    bool Success,
    string? PreviewText = null,
    string? Message = null,
    string? ErrorClass = null,
    int InputLength = 0,
    int OutputLength = 0);
