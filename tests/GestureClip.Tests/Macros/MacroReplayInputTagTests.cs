using System.Reflection;
using System.Runtime.InteropServices;
using GestureClip.Core.Macros;
using GestureClip.Infrastructure.Gestures;
using GestureClip.Infrastructure.Macros;
using GestureClip.Infrastructure.Win32;
using Xunit;

namespace GestureClip.Tests.Macros;

public sealed class MacroReplayInputTagTests
{
    [Theory]
    [InlineData(MacroEventKind.Move)]
    [InlineData(MacroEventKind.RightDown)]
    [InlineData(MacroEventKind.RightUp)]
    public void Replay_mouse_input_is_tagged_and_does_not_reenter_gesture_recognition(MacroEventKind kind)
    {
        var method = typeof(WindowsMacroInputSink).GetMethod("BuildInputs", BindingFlags.NonPublic | BindingFlags.Static)!;
        var inputs = (KeyboardInputNativeMethods.INPUT[])method.Invoke(null, [new MacroEvent { Kind = kind }])!;
        foreach (var input in inputs)
        {
            Assert.NotEqual(0, input.u.mi.dwExtraInfo);
            Assert.False(RecognizedAsMouseEvent((nuint)input.u.mi.dwExtraInfo, true));
        }
        Assert.True(RecognizedAsMouseEvent(0, true)); // Third-party injected input keeps its existing behavior.
    }

    private static bool RecognizedAsMouseEvent(nuint tag, bool injected)
    {
        var data = new MouseHookNativeMethods.MSLLHOOKSTRUCT
            { dwExtraInfo = tag, flags = injected ? MouseHookNativeMethods.LlmhfInjected : 0 };
        var pointer = Marshal.AllocHGlobal(Marshal.SizeOf<MouseHookNativeMethods.MSLLHOOKSTRUCT>());
        try
        {
            Marshal.StructureToPtr(data, pointer, false);
            var method = typeof(LowLevelMouseHook).GetMethod("TryCreateEvent", BindingFlags.NonPublic | BindingFlags.Static)!;
            return (bool)method.Invoke(null, [(nint)MouseHookNativeMethods.WmRButtonDown, pointer, null])!;
        }
        finally { Marshal.FreeHGlobal(pointer); }
    }
}
