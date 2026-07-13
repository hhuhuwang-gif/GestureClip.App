using GestureClip.Core.Gestures;

namespace GestureClip.Core.Abstractions;

public interface IGesturePresetProvider
{
    BuiltInGestureAction GetAction(GesturePreset preset, string pattern);

    BuiltInGestureAction GetAction(GesturePreset preset, GestureExecutionContext context)
    {
        return GetAction(preset, context.Pattern);
    }

    IReadOnlyDictionary<string, BuiltInGestureAction> GetBindings(GesturePreset preset);

    void UpdateCustomBindings(IReadOnlyDictionary<string, BuiltInGestureAction> bindings);

    IReadOnlyDictionary<string, BuiltInGestureAction> GetLeftButtonEnhancedBindings();

    void UpdateLeftButtonEnhancedBindings(IReadOnlyDictionary<string, BuiltInGestureAction> bindings);
}
