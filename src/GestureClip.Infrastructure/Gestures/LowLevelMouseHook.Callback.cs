using System.Runtime.InteropServices;
using GestureClip.Core.Gestures;
using GestureClip.Infrastructure.Win32;
using Microsoft.Extensions.Logging;

namespace GestureClip.Infrastructure.Gestures;

public sealed partial class LowLevelMouseHook
{
    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        try
        {
            if (nCode >= 0 && TryCreateEvent(wParam, lParam, out var hookEvent))
            {
                LogHookEvent(hookEvent);
                var args = new MouseHookEventArgs { Event = hookEvent };
                MouseEventReceived?.Invoke(this, args);
                if (args.Suppress)
                {
                    return 1;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Low-level mouse hook callback failed.");
        }

        return MouseHookNativeMethods.CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
    }

    private void LogHookEvent(MouseHookEvent hookEvent)
    {
        if (hookEvent.Type == MouseHookEventType.Move || !_logger.IsEnabled(LogLevel.Debug))
        {
            return;
        }

        var count = Interlocked.Increment(ref _receivedEventCount);
        _logger.LogDebug(
            "Low-level mouse hook event received: {Type}, x={X}, y={Y}, injected={Injected}, count={Count}",
            hookEvent.Type,
            hookEvent.X,
            hookEvent.Y,
            hookEvent.IsInjected,
            count);
    }

    private static bool TryCreateEvent(IntPtr wParam, IntPtr lParam, out MouseHookEvent hookEvent)
    {
        var message = wParam.ToInt32();
        var data = Marshal.PtrToStructure<MouseHookNativeMethods.MSLLHOOKSTRUCT>(lParam);
        var xButton = data.mouseData >> 16;
        var type = ToEventType(message, xButton);
        if (type is null)
        {
            hookEvent = default!;
            return false;
        }

        var isInjected = (data.flags & MouseHookNativeMethods.LlmhfInjected) != 0;
        var wheelDelta = type == MouseHookEventType.Wheel ? (short)xButton : 0;
        hookEvent = new MouseHookEvent(type.Value, data.pt.x, data.pt.y, DateTimeOffset.UtcNow, isInjected, wheelDelta);
        return true;
    }

    private static MouseHookEventType? ToEventType(int message, uint xButton)
    {
        return message switch
        {
            MouseHookNativeMethods.WmLButtonDown => MouseHookEventType.LeftButtonDown,
            MouseHookNativeMethods.WmLButtonUp => MouseHookEventType.LeftButtonUp,
            MouseHookNativeMethods.WmRButtonDown => MouseHookEventType.RightButtonDown,
            MouseHookNativeMethods.WmMouseMove => MouseHookEventType.Move,
            MouseHookNativeMethods.WmRButtonUp => MouseHookEventType.RightButtonUp,
            MouseHookNativeMethods.WmMButtonDown => MouseHookEventType.MiddleButtonDown,
            MouseHookNativeMethods.WmMButtonUp => MouseHookEventType.MiddleButtonUp,
            MouseHookNativeMethods.WmMouseWheel => MouseHookEventType.Wheel,
            MouseHookNativeMethods.WmXButtonDown when xButton == MouseHookNativeMethods.XButton1 => MouseHookEventType.XButton1Down,
            MouseHookNativeMethods.WmXButtonUp when xButton == MouseHookNativeMethods.XButton1 => MouseHookEventType.XButton1Up,
            MouseHookNativeMethods.WmXButtonDown when xButton == MouseHookNativeMethods.XButton2 => MouseHookEventType.XButton2Down,
            MouseHookNativeMethods.WmXButtonUp when xButton == MouseHookNativeMethods.XButton2 => MouseHookEventType.XButton2Up,
            _ => null
        };
    }
}
