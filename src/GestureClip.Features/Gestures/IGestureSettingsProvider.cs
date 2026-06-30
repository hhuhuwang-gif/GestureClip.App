namespace GestureClip.Features.Gestures;

public interface IGestureSettingsProvider
{
    GestureSettings GetCurrent();

    void Update(GestureSettings settings);
}
