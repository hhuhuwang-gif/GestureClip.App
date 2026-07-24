namespace GestureClip.Core.Clipboard;

public sealed record ClipboardItem(
    Guid Id,
    string ContentType,
    string? TextContent,
    string? PreviewText,
    string Hash,
    string? PlainTextHash,
    string? SourceApp,
    string? SourceProcess,
    bool IsPinned,
    bool IsFavorite,
    bool IsSensitive,
    int UseCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? LastUsedAt,
    string? ThumbnailContent = null,
    string? OcrText = null,
    /// <summary>
    /// UI-only: marked when user copied this item from the overlay this session.
    /// Not persisted; used so multi-select copy can show which rows were already handled.
    /// </summary>
    bool IsSessionCopied = false)
{
    public bool IsText => string.Equals(ContentType, "text", StringComparison.OrdinalIgnoreCase);

    public bool IsImage => ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);

    public string ContentTypeLabel => IsImage
        ? "图片"
        : IsText
            ? "文本"
            : ContentType;
}
