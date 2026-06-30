namespace GestureClip.Core.Abstractions;

public interface IClipboardWriter
{
    Task SetTextAsync(string text, CancellationToken cancellationToken);

    Task SendPasteHotkeyAsync(CancellationToken cancellationToken);
}
