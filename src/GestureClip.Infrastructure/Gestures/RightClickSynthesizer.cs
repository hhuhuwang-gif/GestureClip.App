using System.Runtime.InteropServices;
using GestureClip.Core.Abstractions;
using GestureClip.Infrastructure.Win32;

namespace GestureClip.Infrastructure.Gestures;

public sealed class RightClickSynthesizer : IRightClickSynthesizer
{
    private const uint InputMouse = 0;
    private const uint MouseEventRightDown = 0x0008;
    private const uint MouseEventRightUp = 0x0010;

    public void SynthesizeRightClick(int x, int y)
    {
        MouseHookNativeMethods.SetCursorPos(x, y);
        var inputs = new[]
        {
            MouseInput(MouseEventRightDown),
            MouseInput(MouseEventRightUp)
        };

        MouseHookNativeMethods.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<MouseHookNativeMethods.INPUT>());
    }

    private static MouseHookNativeMethods.INPUT MouseInput(uint flags)
    {
        return new MouseHookNativeMethods.INPUT
        {
            type = InputMouse,
            u = new MouseHookNativeMethods.InputUnion
            {
                mi = new MouseHookNativeMethods.MOUSEINPUT
                {
                    dwFlags = flags
                }
            }
        };
    }
}
