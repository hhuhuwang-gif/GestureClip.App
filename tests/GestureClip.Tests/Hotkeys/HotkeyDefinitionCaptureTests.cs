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

    [Theory]
    [InlineData("Ctrl + Space")]
    [InlineData("Alt + F4")]
    [InlineData("Ctrl + Shift + `")]
    [InlineData("Ctrl + -")]
    [InlineData("Win + Shift + S")]
    public void TryParse_accepts_common_office_combos(string text)
    {
        Assert.True(HotkeyDefinition.TryParse(text, out var hotkey));
        // Display order is normalized to Ctrl, Alt, Shift, Win + key.
        Assert.True(HotkeyDefinition.TryFromVirtualKey(hotkey.Modifiers, hotkey.VirtualKey, out var again));
        Assert.Equal(hotkey.Modifiers, again.Modifiers);
        Assert.Equal(hotkey.VirtualKey, again.VirtualKey);
        Assert.True(HotkeyDefinition.TryParse(again.DisplayText, out var third));
        Assert.Equal(hotkey.Modifiers, third.Modifiers);
        Assert.Equal(hotkey.VirtualKey, third.VirtualKey);
    }

    [Fact]
    public void TryFromVirtualKey_space_and_arrows()
    {
        Assert.True(HotkeyDefinition.TryFromVirtualKey(HotkeyModifier.Control, 0x20, out var space));
        Assert.Equal("Ctrl + Space", space.DisplayText);

        Assert.True(HotkeyDefinition.TryFromVirtualKey(HotkeyModifier.Alt, 0x26, out var up));
        Assert.Equal("Alt + Up", up.DisplayText);
    }
}
