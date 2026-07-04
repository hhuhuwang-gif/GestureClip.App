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
    [InlineData("DL", BuiltInGestureAction.LeftMouseClick)]
    [InlineData("DR", BuiltInGestureAction.RightMouseClick)]
    [InlineData("UR", BuiltInGestureAction.NewTab)]
    [InlineData("UL", BuiltInGestureAction.ReopenClosedTab)]
    [InlineData("RU", BuiltInGestureAction.Refresh)]
    [InlineData("RD", BuiltInGestureAction.CloseTab)]
    [InlineData("LD", BuiltInGestureAction.MinimizeForegroundWindow)]
    [InlineData("RDL", BuiltInGestureAction.Screenshot)]
    [InlineData("RUD", BuiltInGestureAction.ResetZoom)]
    [InlineData("URD", BuiltInGestureAction.NextTab)]
    [InlineData("ULD", BuiltInGestureAction.PreviousTab)]
    [InlineData("RULD", BuiltInGestureAction.SystemSettings)]
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
    [InlineData("DL", BuiltInGestureAction.LeftMouseClick)]
    [InlineData("DR", BuiltInGestureAction.RightMouseClick)]
    [InlineData("UR", BuiltInGestureAction.NewTab)]
    [InlineData("UL", BuiltInGestureAction.ReopenClosedTab)]
    [InlineData("RU", BuiltInGestureAction.Refresh)]
    [InlineData("RD", BuiltInGestureAction.CloseTab)]
    [InlineData("LD", BuiltInGestureAction.MinimizeForegroundWindow)]
    [InlineData("RDL", BuiltInGestureAction.Screenshot)]
    [InlineData("RUD", BuiltInGestureAction.ResetZoom)]
    [InlineData("URD", BuiltInGestureAction.NextTab)]
    [InlineData("ULD", BuiltInGestureAction.PreviousTab)]
    [InlineData("RULD", BuiltInGestureAction.SystemSettings)]
    public void ClipboardEnhanced_maps_patterns_to_clipboard_actions(string pattern, BuiltInGestureAction expected)
    {
        var provider = new GesturePresetProvider();

        Assert.Equal(expected, provider.GetAction(GesturePreset.ClipboardEnhanced, pattern));
    }

    [Fact]
    public void Unknown_pattern_returns_none()
    {
        var provider = new GesturePresetProvider();

        Assert.Equal(BuiltInGestureAction.None, provider.GetAction(GesturePreset.EditEnhanced, "RDR"));
    }

    [Fact]
    public void Custom_bindings_can_override_default_actions()
    {
        var provider = new GesturePresetProvider();

        provider.UpdateCustomBindings(new Dictionary<string, BuiltInGestureAction>
        {
            ["U"] = BuiltInGestureAction.OpenClipboardOverlay,
            ["D"] = BuiltInGestureAction.PasteLatestClipboardItem
        });

        Assert.Equal(BuiltInGestureAction.OpenClipboardOverlay, provider.GetAction(GesturePreset.Custom, "U"));
        Assert.Equal(BuiltInGestureAction.PasteLatestClipboardItem, provider.GetAction(GesturePreset.Custom, "D"));
        Assert.Equal(BuiltInGestureAction.None, provider.GetAction(GesturePreset.Custom, "L"));
    }

    [Fact]
    public void GetBindings_returns_custom_snapshot()
    {
        var provider = new GesturePresetProvider();

        provider.UpdateCustomBindings(new Dictionary<string, BuiltInGestureAction>
        {
            ["LR"] = BuiltInGestureAction.SelectAll
        });

        var bindings = provider.GetBindings(GesturePreset.Custom);

        Assert.Single(bindings);
        Assert.Equal(BuiltInGestureAction.SelectAll, bindings["LR"]);
    }
}
