namespace GestureClip.Core.Abstractions;

public interface IClipboardWriter
{
    Task SetTextAsync(string text, CancellationToken cancellationToken);

    Task SetImagePngBase64Async(string pngBase64, CancellationToken cancellationToken);

    Task SendPasteHotkeyAsync(CancellationToken cancellationToken);
}
