using GestureClip.Core.Clipboard;

namespace GestureClip.Core.Abstractions;

public interface IClipboardSearchIndex
{
    Task<IReadOnlyList<ClipboardSearchEntry>> ReadSearchIndexAsync(
        ClipboardSearchIndexPage page, CancellationToken cancellationToken);

    /// <summary>Loads list presentation fields for matching IDs, without full image Base64.</summary>
    Task<IReadOnlyList<ClipboardItem>> GetDisplayItemsAsync(
        IReadOnlyList<Guid> ids, CancellationToken cancellationToken);
}
