using GestureClip.Core.Gestures;

namespace GestureClip.Features.Gestures;

public sealed record EdgeTriggerSettings(
    bool Enabled,
    int HotZoneSize,
    int DwellMs,
    int CooldownMs,
    BuiltInGestureAction TopLeftAction,
    BuiltInGestureAction TopRightAction,
    BuiltInGestureAction BottomRightAction,
    BuiltInGestureAction BottomLeftAction,
    bool LeftEdgeLeftButtonEnabled,
    BuiltInGestureAction LeftEdgeLeftButtonAction,
    bool LeftEdgeMiddleButtonEnabled,
    BuiltInGestureAction LeftEdgeMiddleButtonAction,
    bool LeftEdgeXButton1Enabled,
    BuiltInGestureAction LeftEdgeXButton1Action,
    bool LeftEdgeXButton2Enabled,
    BuiltInGestureAction LeftEdgeXButton2Action,
    bool TopRightWheelEnabled,
    BuiltInGestureAction TopRightWheelAction)
{
    public BuiltInGestureAction GetAction(ScreenCornerTarget target) => target switch
    {
        ScreenCornerTarget.TopLeft => TopLeftAction,
        ScreenCornerTarget.TopRight => TopRightAction,
        ScreenCornerTarget.BottomRight => BottomRightAction,
        ScreenCornerTarget.BottomLeft => BottomLeftAction,
        _ => BuiltInGestureAction.None
    };

    public BuiltInGestureAction GetLeftEdgeButtonAction(MouseHookEventType eventType) => eventType switch
    {
        MouseHookEventType.LeftButtonDown when LeftEdgeLeftButtonEnabled => LeftEdgeLeftButtonAction,
        MouseHookEventType.MiddleButtonDown when LeftEdgeMiddleButtonEnabled => LeftEdgeMiddleButtonAction,
        MouseHookEventType.XButton1Down when LeftEdgeXButton1Enabled => LeftEdgeXButton1Action,
        MouseHookEventType.XButton2Down when LeftEdgeXButton2Enabled => LeftEdgeXButton2Action,
        _ => BuiltInGestureAction.None
    };
}
