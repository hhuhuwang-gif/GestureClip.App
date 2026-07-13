using GestureClip.Core.Gestures;

namespace GestureClip.Core.Abstractions;

public interface IMouseGestureActionExecutor
{
    Task ExecuteAsync(BuiltInGestureAction action, CancellationToken cancellationToken);

    Task ExecuteAsync(
        BuiltInGestureAction action,
        GestureExecutionContext context,
        CancellationToken cancellationToken)
    {
        return ExecuteAsync(action, cancellationToken);
    }
}
