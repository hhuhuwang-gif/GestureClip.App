using GestureClip.Core.Abstractions;
using GestureClip.Core.Clipboard;

namespace GestureClip.Tests.Support;

/// <summary>For test doubles whose scenarios never exercise clipboard search.</summary>
public abstract class UnsupportedClipboardSearchIndex : IClipboardSearchIndex
{
    public Task<IReadOnlyList<ClipboardSearchEntry>> ReadSearchIndexAsync(
        ClipboardSearchIndexPage page, CancellationToken cancellationToken) =>
        throw new NotSupportedException("This test double does not implement clipboard search indexing.");

    public Task<IReadOnlyList<ClipboardItem>> GetDisplayItemsAsync(
        IReadOnlyList<Guid> ids, CancellationToken cancellationToken) =>
        throw new NotSupportedException("This test double does not implement clipboard search display queries.");
}
