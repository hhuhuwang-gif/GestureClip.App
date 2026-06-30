using GestureClip.Core.Abstractions;
using GestureClip.Core.Gestures;

namespace GestureClip.Features.Gestures;

public sealed class GesturePresetProvider : IGesturePresetProvider
{
    private static readonly IReadOnlyDictionary<string, BuiltInGestureAction> EditEnhanced =
        new Dictionary<string, BuiltInGestureAction>(StringComparer.Ordinal)
        {
            ["U"] = BuiltInGestureAction.Copy,
            ["D"] = BuiltInGestureAction.Paste,
            ["UD"] = BuiltInGestureAction.Enter,
            ["DU"] = BuiltInGestureAction.Escape,
            ["L"] = BuiltInGestureAction.SendAltLeft,
            ["R"] = BuiltInGestureAction.SendAltRight,
            ["LR"] = BuiltInGestureAction.SelectAll,
            ["RL"] = BuiltInGestureAction.Undo
        };

    private static readonly IReadOnlyDictionary<string, BuiltInGestureAction> ClipboardEnhanced =
        new Dictionary<string, BuiltInGestureAction>(StringComparer.Ordinal)
        {
            ["U"] = BuiltInGestureAction.OpenClipboardOverlay,
            ["D"] = BuiltInGestureAction.PasteLatestClipboardItem,
            ["UD"] = BuiltInGestureAction.Enter,
            ["DU"] = BuiltInGestureAction.Escape,
            ["L"] = BuiltInGestureAction.SendAltLeft,
            ["R"] = BuiltInGestureAction.SendAltRight,
            ["LR"] = BuiltInGestureAction.SelectAll,
            ["RL"] = BuiltInGestureAction.Undo
        };

    public BuiltInGestureAction GetAction(GesturePreset preset, string pattern)
    {
        var map = preset switch
        {
            GesturePreset.ClipboardEnhanced => ClipboardEnhanced,
            GesturePreset.EditEnhanced => EditEnhanced,
            GesturePreset.Custom => EditEnhanced,
            _ => EditEnhanced
        };

        return map.TryGetValue(pattern, out var action) ? action : BuiltInGestureAction.None;
    }
}
