using System.Runtime.InteropServices;
using GestureClip.Infrastructure.Win32;
using Xunit;

namespace GestureClip.Tests.Gestures;

public sealed class KeyboardInputNativeMethodsTests
{
    [Fact]
    public void Keyboard_input_struct_matches_win32_input_size_on_x64()
    {
        if (Environment.Is64BitProcess)
        {
            Assert.Equal(40, Marshal.SizeOf<KeyboardInputNativeMethods.INPUT>());
        }
    }
}
