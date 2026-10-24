using GestureClip.Core.Gestures;
using Microsoft.Extensions.Logging;

namespace GestureClip.Features.Gestures;

public sealed partial class EdgeTriggerService
{
    private async Task EvaluateAsync(
        CursorPosition position,
        ScreenBounds bounds,
        EdgeTriggerSettings settings,
        CancellationToken cancellationToken)
    {
        var corner = HitTestCorner(position, bounds, settings.HotZoneSize);
        if (corner is null)
        {
            ResetTracking();
            SetDiagnostics("四角热区", $"{position.X}, {position.Y}", BuiltInGestureAction.None, "未进入角落热区", position.Time);
            return;
        }

        if (position.Time < _cooldownUntil)
        {
            SetDiagnostics($"四角热区 {corner}", $"{position.X}, {position.Y}", BuiltInGestureAction.None, "冷却中", position.Time);
            return;
        }

        if (_activeCorner != corner)
        {
            _activeCorner = corner;
            _enteredCornerAt = position.Time;
            SetDiagnostics($"四角热区 {corner}", $"{position.X}, {position.Y}", BuiltInGestureAction.None, "等待停留", position.Time);
            return;
        }

        if (_enteredCornerAt is null ||
            position.Time - _enteredCornerAt.Value < TimeSpan.FromMilliseconds(settings.DwellMs))
        {
            SetDiagnostics($"四角热区 {corner}", $"{position.X}, {position.Y}", BuiltInGestureAction.None, "停留时间不足", position.Time);
            return;
        }

        var action = settings.GetAction(corner.Value);
        ResetTracking();
        _cooldownUntil = position.Time.AddMilliseconds(settings.CooldownMs);
        if (action == BuiltInGestureAction.None)
        {
            SetDiagnostics($"四角热区 {corner}", $"{position.X}, {position.Y}", BuiltInGestureAction.None, "动作未绑定", position.Time);
            return;
        }

        SetDiagnostics($"四角热区 {corner}", $"{position.X}, {position.Y}", action, "已触发", position.Time);
        _logger.LogInformation("Edge trigger action requested. Corner={Corner}, Action={Action}", corner, action);
        await _actionExecutor.ExecuteAsync(action, cancellationToken);
    }

    private void OnMouseEventReceived(object? sender, MouseHookEventArgs args)
    {
        try
        {
            if (args.Event.IsInjected)
            {
                return;
            }

            BuiltInGestureAction action;
            string source;
            lock (_syncRoot)
            {
                action = ResolveMouseEdgeAction(args.Event, _currentSettings, out source);
                if (action != BuiltInGestureAction.None)
                {
                    _cooldownUntil = args.Event.Time.AddMilliseconds(_currentSettings.CooldownMs);
                    SetDiagnostics(source, $"{args.Event.X}, {args.Event.Y}", action, "已触发", args.Event.Time);
                }
            }

            if (action != BuiltInGestureAction.None)
            {
                _ = Task.Run(() => ExecuteEdgeActionAsync(action, CancellationToken.None));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Edge trigger mouse event handling failed.");
        }
    }

    private async Task ExecuteEdgeActionAsync(BuiltInGestureAction action, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Edge mouse trigger action requested. Action={Action}", action);
            await _actionExecutor.ExecuteAsync(action, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Edge mouse trigger action failed.");
        }
    }
}
