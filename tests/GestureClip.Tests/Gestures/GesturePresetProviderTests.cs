using GestureClip.Core.Gestures;
using GestureClip.Features.Gestures;
using Xunit;

namespace GestureClip.Tests.Gestures;

public sealed class GesturePresetProviderTests
{
    [Theory]
    [InlineData("U", BuiltInGestureAction.Copy)]
    [InlineData("D", BuiltInGestureAction.SmartPaste)]
    [InlineData("UD", BuiltInGestureAction.Enter)]
    [InlineData("DU", BuiltInGestureAction.Escape)]
    [InlineData("L", BuiltInGestureAction.SendAltLeft)]
    [InlineData("R", BuiltInGestureAction.SendAltRight)]
    [InlineData("LR", BuiltInGestureAction.SelectAll)]
    [InlineData("RL", BuiltInGestureAction.Undo)]
    [InlineData("DL", BuiltInGestureAction.PasteAndEnter)]
    [InlineData("DR", BuiltInGestureAction.NewTab)]
    [InlineData("UR", BuiltInGestureAction.SearchSelectedTextWithGoogle)]
    [InlineData("UL", BuiltInGestureAction.SearchSelectedTextWithBaidu)]
    public void EditEnhanced_maps_common_patterns_to_editing_actions(string pattern, BuiltInGestureAction expected)
    {
        var provider = new GesturePresetProvider();

        Assert.Equal(expected, provider.GetAction(GesturePreset.EditEnhanced, pattern));
    }

    [Fact]
    public void EditEnhanced_resolves_left_button_modifier_bindings_before_plain_bindings()
    {
        var provider = new GesturePresetProvider();

        Assert.Equal(
            BuiltInGestureAction.SmartPaste,
            provider.GetAction(GesturePreset.EditEnhanced, new GestureExecutionContext("D", false)));
        Assert.Equal(
            BuiltInGestureAction.SmartPaste,
            provider.GetAction(GesturePreset.EditEnhanced, new GestureExecutionContext("D", true)));
        Assert.Equal(
            BuiltInGestureAction.SelectAll,
            provider.GetAction(GesturePreset.EditEnhanced, new GestureExecutionContext("U", true)));
    }

    [Fact]
    public void Modifier_binding_falls_back_to_plain_action_when_enhanced_action_is_missing()
    {
        var provider = new GesturePresetProvider();

        Assert.Equal(
            BuiltInGestureAction.SelectAll,
            provider.GetAction(GesturePreset.EditEnhanced, new GestureExecutionContext("LR", true)));
    }

    [Fact]
    public void Left_button_enhanced_map_overrides_plain_binding_when_present()
    {
        var provider = new GesturePresetProvider();
        provider.UpdateCustomBindings(new Dictionary<string, BuiltInGestureAction>
        {
            ["D"] = BuiltInGestureAction.Paste
        });

        // Default left enhanced keeps D -> SmartPaste.
        Assert.Equal(
            BuiltInGestureAction.SmartPaste,
            provider.GetAction(GesturePreset.Custom, new GestureExecutionContext("D", true)));

        provider.UpdateLeftButtonEnhancedBindings(new Dictionary<string, BuiltInGestureAction>
        {
            ["D"] = BuiltInGestureAction.Copy
        });
        Assert.Equal(
            BuiltInGestureAction.Copy,
            provider.GetAction(GesturePreset.Custom, new GestureExecutionContext("D", true)));
    }

    [Fact]
    public void Left_button_falls_back_to_plain_when_enhanced_cleared()
    {
        var provider = new GesturePresetProvider();
        provider.UpdateCustomBindings(new Dictionary<string, BuiltInGestureAction>
        {
            ["D"] = BuiltInGestureAction.Paste
        });
        provider.UpdateLeftButtonEnhancedBindings(new Dictionary<string, BuiltInGestureAction>());

        Assert.Equal(
            BuiltInGestureAction.Paste,
            provider.GetAction(GesturePreset.Custom, new GestureExecutionContext("D", true)));
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
    [InlineData("DL", BuiltInGestureAction.PasteAndEnter)]
    [InlineData("DR", BuiltInGestureAction.NewTab)]
    [InlineData("UR", BuiltInGestureAction.SearchSelectedTextWithGoogle)]
    [InlineData("UL", BuiltInGestureAction.SearchSelectedTextWithBaidu)]
    public void ClipboardEnhanced_maps_common_patterns_to_clipboard_actions(string pattern, BuiltInGestureAction expected)
    {
        var provider = new GesturePresetProvider();

        Assert.Equal(expected, provider.GetAction(GesturePreset.ClipboardEnhanced, pattern));
    }

    [Theory]
    [InlineData("RDL")]
    [InlineData("URD")]
    [InlineData("RDR")]
    public void Advanced_or_unknown_patterns_are_unbound_by_default(string pattern)
    {
        var provider = new GesturePresetProvider();

        Assert.Equal(BuiltInGestureAction.None, provider.GetAction(GesturePreset.EditEnhanced, pattern));
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

