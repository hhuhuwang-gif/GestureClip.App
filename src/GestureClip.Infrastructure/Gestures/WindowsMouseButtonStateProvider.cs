using System.Runtime.InteropServices;
using GestureClip.Core.Abstractions;
using GestureClip.Core.Gestures;

namespace GestureClip.Infrastructure.Gestures;

public sealed class WindowsMouseButtonStateProvider : IMouseButtonStateProvider
{
    private const int PressedMask = 0x8000;
    private const int VirtualKeyLeftButton = 0x01;
    private const int VirtualKeyRightButton = 0x02;
    private const int VirtualKeyMiddleButton = 0x04;
    private const int VirtualKeyXButton1 = 0x05;
    private const int VirtualKeyXButton2 = 0x06;

    public bool IsPressed(GestureTriggerButton button) =>
        (GetAsyncKeyState(ToVirtualKey(button)) & PressedMask) != 0;

    private static int ToVirtualKey(GestureTriggerButton button) => button switch
    {
        GestureTriggerButton.Left => VirtualKeyLeftButton,
        GestureTriggerButton.Right => VirtualKeyRightButton,
        GestureTriggerButton.Middle => VirtualKeyMiddleButton,
        GestureTriggerButton.XButton1 => VirtualKeyXButton1,
        GestureTriggerButton.XButton2 => VirtualKeyXButton2,
        _ => throw new ArgumentOutOfRangeException(nameof(button), button, null)
    };

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int virtualKey);
}
