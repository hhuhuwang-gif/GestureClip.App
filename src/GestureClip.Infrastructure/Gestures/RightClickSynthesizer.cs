using System.Runtime.InteropServices;
using GestureClip.Core.Abstractions;
using GestureClip.Core.Gestures;
using GestureClip.Infrastructure.Win32;

namespace GestureClip.Infrastructure.Gestures;

public sealed class RightClickSynthesizer : IRightClickSynthesizer
{
    private const uint InputMouse = 0;
    private const uint MouseEventLeftDown = 0x0002;
    private const uint MouseEventLeftUp = 0x0004;
    private const uint MouseEventMiddleDown = 0x0020;
    private const uint MouseEventMiddleUp = 0x0040;
    private const uint MouseEventRightDown = 0x0008;
    private const uint MouseEventRightUp = 0x0010;
    private const uint MouseEventXDown = 0x0080;
    private const uint MouseEventXUp = 0x0100;

    public void SynthesizeRightClick(int x, int y)
    {
        SynthesizeClick(GestureTriggerButton.Right, x, y);
    }

    public void SynthesizeClick(GestureTriggerButton button, int x, int y)
    {
        MouseHookNativeMethods.SetCursorPos(x, y);
        var (down, up, mouseData) = button switch
        {
            GestureTriggerButton.Left => (MouseEventLeftDown, MouseEventLeftUp, 0u),
            GestureTriggerButton.Middle => (MouseEventMiddleDown, MouseEventMiddleUp, 0u),
            GestureTriggerButton.XButton1 => (MouseEventXDown, MouseEventXUp, MouseHookNativeMethods.XButton1),
            GestureTriggerButton.XButton2 => (MouseEventXDown, MouseEventXUp, MouseHookNativeMethods.XButton2),
            _ => (MouseEventRightDown, MouseEventRightUp, 0u)
        };

        var inputs = new[]
        {
            MouseInput(down, mouseData),
            MouseInput(up, mouseData)
        };

        MouseHookNativeMethods.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<MouseHookNativeMethods.INPUT>());
    }

    private static MouseHookNativeMethods.INPUT MouseInput(uint flags, uint mouseData)
    {
        return new MouseHookNativeMethods.INPUT
        {
            type = InputMouse,
            u = new MouseHookNativeMethods.InputUnion
            {
                mi = new MouseHookNativeMethods.MOUSEINPUT
                {
                    mouseData = mouseData,
                    dwFlags = flags
                }
            }
        };
    }
}
