using System.Runtime.InteropServices;
using GestureClip.Infrastructure.Win32;
using Xunit;

namespace GestureClip.Tests.Updates;

public sealed class KeyboardPasteInjectorTests
{
    [Fact]
    public void BuildFullPasteSequence_is_keyboard_only_no_mouse_ups()
    {
        // Mouse button-ups after right-gesture open the context menu on many apps.
        var events = KeyboardPasteInjector.BuildFullPasteSequence();
        Assert.True(events.Length >= 10);
        Assert.All(events, e => Assert.Equal(1u, e.type)); // keyboard only

        const uint keyUp = 0x0002;
        var first = events[0];
        Assert.Equal(keyUp, first.u.ki.dwFlags & keyUp);

        // Ends with Ctrl up
        var last = events[^1];
        Assert.Equal(1u, last.type);
        Assert.Equal(0x11, last.u.ki.wVk);
        Assert.Equal(keyUp, last.u.ki.dwFlags & keyUp);

        Assert.Contains(events, e => e.type == 1 && e.u.ki.wVk == 0x56);
    }

    [Fact]
    public void BuildModifierReleaseOnly_only_contains_keyups()
    {
        var events = KeyboardPasteInjector.BuildModifierReleaseOnly();
        Assert.Equal(KeyboardPasteInjector.ModifierVirtualKeys.Length, events.Length);
        Assert.All(events, e =>
        {
            Assert.Equal(1u, e.type);
            Assert.Equal(0x0002u, e.u.ki.dwFlags & 0x0002u);
        });
    }

    [Fact]
    public void Input_struct_size_is_valid_for_x64_SendInput()
    {
        // On Windows x64, INPUT is typically 40 bytes. On x86 it is 28.
        var size = Marshal.SizeOf<KeyboardInputNativeMethods.INPUT>();
        Assert.True(size is 28 or 40, $"Unexpected INPUT size: {size}");

        var clipSize = Marshal.SizeOf<ClipboardNativeMethods.INPUT>();
        Assert.Equal(size, clipSize);
    }

    [Fact]
    public void BuildPasteWithModifierRelease_legacy_api_still_usable()
    {
        var events = KeyboardPasteInjector.BuildPasteWithModifierRelease();
        Assert.True(events.Length >= 10);
        Assert.Contains(events, e => e.u.ki.wVk == 0x56);
    }

    [Fact]
    public void BuildFullPasteSequence_key_events_include_scan_codes()
    {
        var events = KeyboardPasteInjector.BuildFullPasteSequence();
        var keyEvents = events.Where(e => e.type == 1 && (e.u.ki.dwFlags & 0x0002u) == 0).ToArray();
        Assert.Contains(keyEvents, e => e.u.ki.wVk == 0x11); // Ctrl down
        Assert.Contains(keyEvents, e => e.u.ki.wVk == 0x56); // V down
        // Scan codes should be populated for real keys
        Assert.All(keyEvents.Where(e => e.u.ki.wVk is 0x11 or 0x56), e => Assert.True(e.u.ki.wScan != 0));
    }

    [Fact]
    public void BuildMouseButtonUps_covers_primary_buttons()
    {
        var ups = KeyboardPasteInjector.BuildMouseButtonUps();
        Assert.Equal(5, ups.Length);
        Assert.All(ups, e => Assert.Equal(0u, e.type));
    }
}
