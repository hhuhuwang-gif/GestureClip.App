using GestureClip.Core.Gestures;
using GestureClip.Features.Gestures;
using Xunit;

namespace GestureClip.Tests.Gestures;

public sealed class GesturePresetProviderTests
{
    [Theory]
    [InlineData("U", BuiltInGestureAction.Copy)]
    [InlineData("D", BuiltInGestureAction.Paste)]
    [InlineData("UD", BuiltInGestureAction.Enter)]
    [InlineData("DU", BuiltInGestureAction.Escape)]
    [InlineData("L", BuiltInGestureAction.SendAltLeft)]
    [InlineData("R", BuiltInGestureAction.SendAltRight)]
    [InlineData("LR", BuiltInGestureAction.SelectAll)]
    [InlineData("RL", BuiltInGestureAction.Undo)]
    public void EditEnhanced_maps_patterns_to_editing_actions(string pattern, BuiltInGestureAction expected)
    {
        var provider = new GesturePresetProvider();

        Assert.Equal(expected, provider.GetAction(GesturePreset.EditEnhanced, pattern));
    }

    [Theory]
    [InlineData("U", BuiltInGestureAction.OpenClipboardOverlay)]
    [InlineData("D", BuiltInGestureAction.PasteLatestClipboardItem)]
    [InlineData("UD", BuiltInGestureAction.Enter)]
    [InlineData("DU", BuiltInGestureAction.Escape)]
    [InlineData("L", BuiltInGestureAction.SendAltLeft)]
    [InlineData("R", BuiltInGestureAction.SendAltRight)]
    [InlineData("LR", BuiltInGestureAction.SelectAll)]
    [InlineData("RL", BuiltInGestureAction.Undo)]
    public void ClipboardEnhanced_maps_patterns_to_clipboard_actions(string pattern, BuiltInGestureAction expected)
    {
        var provider = new GesturePresetProvider();

        Assert.Equal(expected, provider.GetAction(GesturePreset.ClipboardEnhanced, pattern));
    }

    [Fact]
    public void Unknown_pattern_returns_none()
    {
        var provider = new GesturePresetProvider();

        Assert.Equal(BuiltInGestureAction.None, provider.GetAction(GesturePreset.EditEnhanced, "LD"));
    }
}
