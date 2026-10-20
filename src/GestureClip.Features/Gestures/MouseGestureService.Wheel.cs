using GestureClip.Core.Abstractions;
using GestureClip.Core.Gestures;
using GestureClip.Infrastructure.Win32;
using Microsoft.Extensions.Logging;

namespace GestureClip.Features.Gestures;

public sealed partial class MouseGestureService : IMouseGestureService
{
    private PendingGesture? HandleWheel(MouseHookEventArgs args)
    {
        var mouseEvent = args.Event;
        // Wheel gesture: scroll while holding the trigger button. Fires per notch
        // (repeatable, e.g. tab switching); release then does nothing extra.
        if (_state == GestureRuntimeState.Idle ||
            _activeSettings is null ||
            !_activeSettings.WheelGestureEnabled)
        {
            return null;
        }

        args.Suppress = true;
        _wheelGestureFired = true;
        var wheelAction = mouseEvent.WheelDelta > 0
            ? _activeSettings.WheelUpAction
            : _activeSettings.WheelDownAction;
        var wheelContext = new GestureExecutionContext(
            mouseEvent.WheelDelta > 0 ? "WHEEL_UP" : "WHEEL_DOWN",
            _leftButtonModifier,
            _gestureTargetHwnd,
            _gestureTargetProcessName);
        var action = _presetProvider.GetWheelAction(wheelContext, wheelAction);
        _lastPattern = wheelContext.Pattern;
        _lastAction = action;
        if (action != BuiltInGestureAction.None)
            _ = Task.Run(() => _actionExecutor.ExecuteAsync(action, wheelContext, CancellationToken.None));

        return null;
    }
}
