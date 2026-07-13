using GestureClip.Infrastructure.Win32;
using Xunit;

namespace GestureClip.Tests.Updates;

public sealed class KeyboardPasteInjectorTests
{
    [Fact]
    public void BuildPasteWithModifierRelease_starts_with_modifier_keyups_then_ctrl_v()
    {
        var events = KeyboardPasteInjector.BuildPasteWithModifierRelease();

        Assert.True(events.Length >= 15);

        // First events must be key-ups for modifiers (flag bit 0x0002).
        const uint keyUp = 0x0002;
        for (var i = 0; i < KeyboardPasteInjector.ModifierVirtualKeys.Length; i++)
        {
            Assert.Equal(keyUp, events[i].u.ki.dwFlags);
            Assert.Equal(KeyboardPasteInjector.ModifierVirtualKeys[i], events[i].u.ki.wVk);
        }

        var pasteStart = KeyboardPasteInjector.ModifierVirtualKeys.Length;
        Assert.Equal(0u, events[pasteStart].u.ki.dwFlags); // Ctrl down
        Assert.Equal(0x11, events[pasteStart].u.ki.wVk);
        Assert.Equal(0x56, events[pasteStart + 1].u.ki.wVk); // V down
        Assert.Equal(keyUp, events[pasteStart + 2].u.ki.dwFlags); // V up
        Assert.Equal(keyUp, events[pasteStart + 3].u.ki.dwFlags); // Ctrl up
    }

    [Fact]
    public void BuildModifierReleaseOnly_only_contains_keyups()
    {
        var events = KeyboardPasteInjector.BuildModifierReleaseOnly();
        Assert.Equal(KeyboardPasteInjector.ModifierVirtualKeys.Length, events.Length);
        Assert.All(events, e => Assert.Equal(0x0002u, e.u.ki.dwFlags));
    }
}
