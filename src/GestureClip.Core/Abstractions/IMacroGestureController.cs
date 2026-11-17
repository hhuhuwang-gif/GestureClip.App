using GestureClip.Core.Gestures;

namespace GestureClip.Core.Abstractions;

public interface IMacroGestureController
{
    Task ExecuteGestureAsync(BuiltInGestureAction action, GestureExecutionContext context, CancellationToken token);
}
