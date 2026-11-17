using GestureClip.Core.Clipboard;

namespace GestureClip.Features.Clipboard;

public sealed partial class ClipboardService
{
    public Task<IReadOnlyList<ClipboardSearchEntry>> ReadSearchIndexAsync(
        ClipboardSearchIndexPage page, CancellationToken cancellationToken) =>
        _clipboardRepository.ReadSearchIndexAsync(page, cancellationToken);

    public Task<IReadOnlyList<ClipboardItem>> GetDisplayItemsAsync(
        IReadOnlyList<Guid> ids, CancellationToken cancellationToken) =>
        _clipboardRepository.GetDisplayItemsAsync(ids, cancellationToken);
}
