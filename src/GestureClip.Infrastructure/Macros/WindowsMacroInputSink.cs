using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using GestureClip.Core.Abstractions;
using GestureClip.Core.Macros;
using Native = GestureClip.Infrastructure.Win32.KeyboardInputNativeMethods;

namespace GestureClip.Infrastructure.Macros;

public sealed class WindowsMacroInputSink : IMacroInputSink
{
    private const uint MouseMove = 0x0001, VirtualDesktop = 0x4000, Absolute = 0x8000;
    private const uint ScanCodeFlag = 0x0008, ExtendedFlag = 0x0001;
    private const int VirtualX = 76, VirtualY = 77, VirtualWidth = 78, VirtualHeight = 79;
    private const double AbsoluteRange = 65535d;

    public void Send(MacroEvent input)
    {
        var events = BuildInputs(input);
        if (Native.SendInput((uint)events.Length, events, Marshal.SizeOf<Native.INPUT>()) != events.Length)
            throw new Win32Exception(Marshal.GetLastWin32Error(), "宏输入发送失败；目标窗口权限或输入桌面可能不允许回放。");
    }

    private static Native.INPUT[] BuildInputs(MacroEvent input)
    {
        if (input.Kind is MacroEventKind.KeyDown or MacroEventKind.KeyUp)
            return [new Native.INPUT
            {
                type = Native.InputKeyboard,
                u = new Native.InputUnion { ki = new Native.KEYBDINPUT
                {
                    wVk = input.ScanCode == 0 ? (ushort)input.Code : (ushort)0,
                    wScan = (ushort)input.ScanCode,
                    dwExtraInfo = (nint)MacroNative.ReplayInputTag,
                    dwFlags = (input.ScanCode == 0 ? 0 : ScanCodeFlag) | (input.Extended ? ExtendedFlag : 0)
                        | (input.Kind == MacroEventKind.KeyUp ? Native.KeyEventKeyUp : 0)
                } }
            }];

        var move = MouseInput(new Native.MOUSEINPUT
        {
            dx = Normalize(input.X, VirtualX, VirtualWidth), dy = Normalize(input.Y, VirtualY, VirtualHeight),
            dwFlags = MouseMove | Absolute | VirtualDesktop
        });
        if (input.Kind == MacroEventKind.Move) return [move];
        var button = MouseInput(new Native.MOUSEINPUT { dwFlags = MouseFlags(input.Kind), mouseData = input.Code });
        return input.KeepCursorPosition ? [button] : [move, button];
    }

    private static int Normalize(int value, int originMetric, int sizeMetric)
    {
        var span = MacroNative.GetSystemMetrics(sizeMetric);
        if (span <= 1) throw new InvalidOperationException("无法确定虚拟桌面尺寸。");
        return (int)Math.Round((value - MacroNative.GetSystemMetrics(originMetric)) * AbsoluteRange / (span - 1));
    }

    private static Native.INPUT MouseInput(Native.MOUSEINPUT mouse)
    {
        mouse.dwExtraInfo = (nint)MacroNative.ReplayInputTag;
        return new Native.INPUT { u = new Native.InputUnion { mi = mouse } };
    }

    private static uint MouseFlags(MacroEventKind kind) => kind switch
    {
        MacroEventKind.LeftDown => 0x0002, MacroEventKind.LeftUp => 0x0004,
        MacroEventKind.RightDown => 0x0008, MacroEventKind.RightUp => 0x0010,
        MacroEventKind.MiddleDown => 0x0020, MacroEventKind.MiddleUp => 0x0040,
        MacroEventKind.Wheel => 0x0800, MacroEventKind.HorizontalWheel => 0x1000,
        MacroEventKind.XDown => 0x0080, MacroEventKind.XUp => 0x0100,
        _ => throw new InvalidDataException("不支持的鼠标事件。")
    };

    public static async Task WaitForReplayKeyReleaseAsync(CancellationToken token)
    {
        const int replayKey = (int)MacroNative.F9;
        const int keyCheckIntervalMs = 10;
        while (MacroNative.GetAsyncKeyState(replayKey) < 0) await Task.Delay(keyCheckIntervalMs, token);
    }
}
