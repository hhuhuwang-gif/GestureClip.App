using GestureClip.Core.Gestures;

namespace GestureClip.Core.Abstractions;

public interface IGesturePresetProvider
{
    BuiltInGestureAction GetAction(GesturePreset preset, string pattern);
}
