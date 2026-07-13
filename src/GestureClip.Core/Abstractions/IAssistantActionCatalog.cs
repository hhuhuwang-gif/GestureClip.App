using GestureClip.Core.Assistant;

namespace GestureClip.Core.Abstractions;

public interface IAssistantActionCatalog
{
    IReadOnlyList<AssistantActionDefinition> GetActions();

    AssistantActionDefinition? GetById(string actionId);
}
