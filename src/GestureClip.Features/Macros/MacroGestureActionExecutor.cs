using GestureClip.Core.Abstractions;
using GestureClip.Core.Gestures;

namespace GestureClip.Features.Macros;

public sealed class MacroGestureActionExecutor(IMouseGestureActionExecutor inner, IMacroGestureController macros)
    : IMouseGestureActionExecutor
{
    public Task ExecuteAsync(BuiltInGestureAction action, CancellationToken cancellationToken)
        => ExecuteAsync(action, new GestureExecutionContext("", false), cancellationToken);

    public Task ExecuteAsync(BuiltInGestureAction action, GestureExecutionContext context, CancellationToken cancellationToken)
        => action is BuiltInGestureAction.MacroToggleRecording or BuiltInGestureAction.MacroTogglePlayback
            ? macros.ExecuteGestureAsync(action, context, cancellationToken)
            : inner.ExecuteAsync(action, context, cancellationToken);
}
