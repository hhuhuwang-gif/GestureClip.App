using GestureClip.Core.Gestures;

namespace GestureClip.Core.Abstractions;

public interface IGestureHudInfoProvider
{
    GestureHudInfo GetInfo(GesturePreset preset, string? pattern);

    GestureHudInfo GetInfo(GesturePreset preset, GestureExecutionContext context)
    {
        return GetInfo(preset, context.Pattern);
    }
}
