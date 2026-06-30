using GestureClip.Core.Gestures;

namespace GestureClip.Core.Abstractions;

public interface IGestureHudInfoProvider
{
    GestureHudInfo GetInfo(GesturePreset preset, string? pattern);
}
