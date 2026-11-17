namespace GestureClip.Core.Clipboard;

/// <summary>Text-only search projection; deliberately excludes image payloads and thumbnails.</summary>
public sealed record ClipboardSearchEntry(Guid Id, bool IsText, string? TextContent, string? PreviewText, string? OcrText)
{
    public static ClipboardSearchEntry FromItem(ClipboardItem item) =>
        new(item.Id, item.IsText, item.IsImage ? null : item.TextContent, item.PreviewText, item.OcrText);
}

public sealed record ClipboardSearchIndexPage(int Limit, int Offset, ClipboardContentFilter Filter);
