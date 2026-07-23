using GestureClip.Core.Hotkeys;
using Xunit;

namespace GestureClip.Tests.Hotkeys;

public sealed class HotkeyDefinitionCaptureTests
{
    [Fact]
    public void TryFromVirtualKey_builds_ctrl_shift_q()
    {
        var ok = HotkeyDefinition.TryFromVirtualKey(
            HotkeyModifier.Control | HotkeyModifier.Shift,
            (uint)'Q',
            out var hotkey);

        Assert.True(ok);
        Assert.Equal("Ctrl + Shift + Q", hotkey.DisplayText);
        Assert.True(HotkeyDefinition.TryParse(hotkey.DisplayText, out var parsed));
        Assert.Equal(hotkey.DisplayText, parsed.DisplayText);
    }

    [Fact]
    public void TryFromVirtualKey_rejects_modifier_only()
    {
        Assert.False(HotkeyDefinition.TryFromVirtualKey(HotkeyModifier.Control, 0x11, out _));
        Assert.False(HotkeyDefinition.TryFromVirtualKey(0, (uint)'A', out _));
    }

    [Fact]
    public void TryParse_accepts_function_key()
    {
        Assert.True(HotkeyDefinition.TryParse("Ctrl + F2", out var hotkey));
        Assert.Equal("Ctrl + F2", hotkey.DisplayText);
    }
}
