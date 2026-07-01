using GestureClip.Core.Gestures;

namespace GestureClip.Core.Abstractions;

public interface IGesturePresetProvider
{
    BuiltInGestureAction GetAction(GesturePreset preset, string pattern);

    IReadOnlyDictionary<string, BuiltInGestureAction> GetBindings(GesturePreset preset);

    void UpdateCustomBindings(IReadOnlyDictionary<string, BuiltInGestureAction> bindings);
}
