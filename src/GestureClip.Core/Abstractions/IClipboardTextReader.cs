namespace GestureClip.Core.Abstractions;

public interface IClipboardTextReader
{
    string? TryReadText();

    string? TryReadImagePngBase64();
}
