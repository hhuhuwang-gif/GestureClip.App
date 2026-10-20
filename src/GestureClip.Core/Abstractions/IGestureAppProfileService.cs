using GestureClip.Core.Gestures;

namespace GestureClip.Core.Abstractions;

public interface IGestureAppProfileService
{
    IReadOnlyList<GestureAppProfile> GetCurrent();

    Task SaveAsync(IReadOnlyList<GestureAppProfile> profiles, CancellationToken cancellationToken);

    bool TryGetAction(GestureExecutionContext context, out BuiltInGestureAction action);
}
