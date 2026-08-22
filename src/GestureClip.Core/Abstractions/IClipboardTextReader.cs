using GestureClip.Core.Clipboard;

namespace GestureClip.Core.Abstractions;

public interface IClipboardTextReader
{
    string? TryReadText();

    string? TryReadImagePngBase64();

    ClipboardReadSnapshot TryReadSnapshot()
    {
        var text = TryReadText();
        if (!string.IsNullOrWhiteSpace(text))
        {
            return new ClipboardReadSnapshot(text, null);
        }

        var image = TryReadImagePngBase64();
        return new ClipboardReadSnapshot(null, image, image is not null);
    }
}
