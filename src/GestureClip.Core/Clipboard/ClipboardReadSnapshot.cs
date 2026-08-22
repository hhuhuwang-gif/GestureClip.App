namespace GestureClip.Core.Clipboard;

/// <summary>
/// One coherent read of the Windows clipboard. PreferImage is true only when the
/// source exposes an explicit image payload rather than an automatic bitmap conversion.
/// </summary>
public sealed record ClipboardReadSnapshot(
    string? Text,
    string? ImagePngBase64,
    bool PreferImage = false);
