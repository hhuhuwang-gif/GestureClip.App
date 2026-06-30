using GestureClip.Core.Clipboard;

namespace GestureClip.Core.Abstractions;

public interface IClipboardListener
{
    event EventHandler<ClipboardChangedEventArgs>? ClipboardChanged;

    void Start();

    void Stop();
}
