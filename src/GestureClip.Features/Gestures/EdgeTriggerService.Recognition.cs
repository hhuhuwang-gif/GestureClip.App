using GestureClip.Core.Gestures;

namespace GestureClip.Features.Gestures;

public sealed partial class EdgeTriggerService
{
    private BuiltInGestureAction ResolveMouseEdgeAction(
        MouseHookEvent mouseEvent,
        EdgeTriggerSettings settings,
        out string source)
    {
        source = "边缘触发";
        if (!settings.Enabled || mouseEvent.Time < _cooldownUntil)
        {
            SetDiagnostics(source, $"{mouseEvent.X}, {mouseEvent.Y}", BuiltInGestureAction.None, settings.Enabled ? "冷却中" : "边缘触发未启用", mouseEvent.Time);
            return BuiltInGestureAction.None;
        }

        var bounds = _cursorPositionProvider.GetVirtualScreenBounds();
        var slideAction = ResolveSlideAction(mouseEvent, bounds, settings, out source);
        if (slideAction != BuiltInGestureAction.None)
        {
            return slideAction;
        }

        if (mouseEvent.Type == MouseHookEventType.Wheel)
        {
            source = "右上角 + 滚轮";
            if (!settings.TopRightWheelEnabled || !IsTopRightCorner(mouseEvent.X, mouseEvent.Y, bounds, settings.HotZoneSize))
            {
                SetDiagnostics(source, $"{mouseEvent.X}, {mouseEvent.Y}", BuiltInGestureAction.None, settings.TopRightWheelEnabled ? "未进入右上角热区" : "触发方式未启用", mouseEvent.Time);
                return BuiltInGestureAction.None;
            }

            return settings.TopRightWheelAction;
        }

        source = mouseEvent.Type switch
        {
            MouseHookEventType.MiddleButtonDown => "左边缘 + 鼠标中键",
            MouseHookEventType.XButton1Down => "左边缘 + 鼠标侧键 1",
            MouseHookEventType.XButton2Down => "左边缘 + 鼠标侧键 2",
            _ => "左边缘 + 鼠标按钮"
        };
        if (!IsLeftEdge(mouseEvent.X, bounds, settings.HotZoneSize))
        {
            if (IsButtonDown(mouseEvent.Type))
            {
                SetDiagnostics(source, $"{mouseEvent.X}, {mouseEvent.Y}", BuiltInGestureAction.None, "未进入左边缘热区", mouseEvent.Time);
            }

            return BuiltInGestureAction.None;
        }

        return settings.GetLeftEdgeButtonAction(mouseEvent.Type);
    }

    private BuiltInGestureAction ResolveSlideAction(
        MouseHookEvent mouseEvent,
        ScreenBounds bounds,
        EdgeTriggerSettings settings,
        out string source)
    {
        source = "边缘滑动";
        if (mouseEvent.Type != MouseHookEventType.Move)
        {
            return BuiltInGestureAction.None;
        }

        var position = new CursorPosition(mouseEvent.X, mouseEvent.Y, mouseEvent.Time);
        var edge = HitTestEdge(position, bounds, settings.HotZoneSize);
        if (_activeSlideEdge is null)
        {
            if (edge is not null)
            {
                _activeSlideEdge = edge;
                _slideStartPoint = position;
                SetDiagnostics($"边缘滑动 {edge}", $"{position.X}, {position.Y}", BuiltInGestureAction.None, "等待向屏幕内滑动", position.Time);
            }

            return BuiltInGestureAction.None;
        }

        if (_slideStartPoint is null)
        {
            ResetSlideTracking();
            return BuiltInGestureAction.None;
        }

        source = $"边缘滑动 {_activeSlideEdge}";
        var distance = InwardDistance(_activeSlideEdge.Value, _slideStartPoint, position);
        if (distance < settings.SlideThreshold)
        {
            SetDiagnostics(source, $"{position.X}, {position.Y}", BuiltInGestureAction.None, $"滑动距离不足 {distance}px", position.Time);
            return BuiltInGestureAction.None;
        }

        var action = settings.GetSlideAction(_activeSlideEdge.Value);
        ResetSlideTracking();
        return action;
    }

    private static ScreenCornerTarget? HitTestCorner(CursorPosition position, ScreenBounds bounds, int hotZoneSize)
    {
        var nearLeft = position.X <= bounds.Left + hotZoneSize;
        var nearRight = position.X >= bounds.Right - hotZoneSize;
        var nearTop = position.Y <= bounds.Top + hotZoneSize;
        var nearBottom = position.Y >= bounds.Bottom - hotZoneSize;

        if (nearLeft && nearTop) return ScreenCornerTarget.TopLeft;
        if (nearRight && nearTop) return ScreenCornerTarget.TopRight;
        if (nearRight && nearBottom) return ScreenCornerTarget.BottomRight;
        if (nearLeft && nearBottom) return ScreenCornerTarget.BottomLeft;
        return null;
    }

    private static bool IsLeftEdge(int x, ScreenBounds bounds, int hotZoneSize) =>
        x <= bounds.Left + hotZoneSize;

    private static bool IsTopRightCorner(int x, int y, ScreenBounds bounds, int hotZoneSize) =>
        x >= bounds.Right - hotZoneSize && y <= bounds.Top + hotZoneSize;

    private static bool IsButtonDown(MouseHookEventType type) =>
        type is MouseHookEventType.LeftButtonDown or MouseHookEventType.MiddleButtonDown or
            MouseHookEventType.XButton1Down or MouseHookEventType.XButton2Down;

    private static ScreenEdgeTarget? HitTestEdge(CursorPosition position, ScreenBounds bounds, int hotZoneSize)
    {
        if (position.X <= bounds.Left + hotZoneSize) return ScreenEdgeTarget.Left;
        if (position.X >= bounds.Right - hotZoneSize) return ScreenEdgeTarget.Right;
        if (position.Y <= bounds.Top + hotZoneSize) return ScreenEdgeTarget.Top;
        if (position.Y >= bounds.Bottom - hotZoneSize) return ScreenEdgeTarget.Bottom;
        return null;
    }

    private static int InwardDistance(ScreenEdgeTarget edge, CursorPosition start, CursorPosition current)
    {
        return edge switch
        {
            ScreenEdgeTarget.Left => current.X - start.X,
            ScreenEdgeTarget.Right => start.X - current.X,
            ScreenEdgeTarget.Top => current.Y - start.Y,
            ScreenEdgeTarget.Bottom => start.Y - current.Y,
            _ => 0
        };
    }
}
