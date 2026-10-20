namespace GestureClip.Core.Abstractions;

public interface IClipboardWriter
{
    Task SetTextAsync(string text, CancellationToken cancellationToken);

    Task SetImagePngBase64Async(string pngBase64, CancellationToken cancellationToken);

    Task SendPasteHotkeyAsync(CancellationToken cancellationToken);

    Task SendPasteHotkeyAsync(nint targetWindow, CancellationToken cancellationToken) => targetWindow == 0
        ? SendPasteHotkeyAsync(cancellationToken)
        : throw new NotSupportedException("当前剪贴板写入器不支持指定粘贴目标。");
}
