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
    BuiltInGestureAction BottomLeftAction)
{
    public BuiltInGestureAction GetAction(ScreenCornerTarget target) => target switch
    {
        ScreenCornerTarget.TopLeft => TopLeftAction,
        ScreenCornerTarget.TopRight => TopRightAction,
        ScreenCornerTarget.BottomRight => BottomRightAction,
        ScreenCornerTarget.BottomLeft => BottomLeftAction,
        _ => BuiltInGestureAction.None
    };
}
