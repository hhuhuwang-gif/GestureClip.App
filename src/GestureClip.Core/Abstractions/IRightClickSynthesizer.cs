namespace GestureClip.Core.Abstractions;

using GestureClip.Core.Gestures;

public interface IRightClickSynthesizer
{
    void SynthesizeRightClick(int x, int y);

    void SynthesizeClick(GestureTriggerButton button, int x, int y);
}
