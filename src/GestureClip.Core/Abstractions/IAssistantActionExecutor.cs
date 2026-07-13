using GestureClip.Core.Assistant;

namespace GestureClip.Core.Abstractions;

public interface IAssistantActionExecutor
{
    Task<AssistantActionResult> ExecuteAsync(AssistantActionRequest request, CancellationToken cancellationToken);
}
