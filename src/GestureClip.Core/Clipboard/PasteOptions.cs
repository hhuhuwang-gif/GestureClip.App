namespace GestureClip.Core.Clipboard;

public sealed record PasteOptions(bool PlainText)
{
    public nint TargetWindowHandle { get; init; }
}
