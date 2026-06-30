using GestureClip.Core.Gestures;

namespace GestureClip.Core.Abstractions;

public interface IMouseGestureActionExecutor
{
    Task ExecuteAsync(BuiltInGestureAction action, CancellationToken cancellationToken);
}
