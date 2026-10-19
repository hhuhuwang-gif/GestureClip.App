using GestureClip.Core.Abstractions;
using GestureClip.Core.Clipboard;
using GestureClip.Core.Settings;
using GestureClip.Core.SystemInfo;
using GestureClip.Features.Clipboard;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using GestureClip.Tests.TestDoubles;

namespace GestureClip.Tests.Clipboard;

public sealed partial class ClipboardServiceTests
{

    [Fact]
    public async Task UpdateTextContentAsync_edits_text_item_in_place()
    {
        var repository = new FakeClipboardRepository();
        var service = CreateService(repository);
        await service.CaptureTextAsync(Capture("original"), CancellationToken.None);
        var item = Assert.Single(repository.Items);

        var updated = await service.UpdateTextContentAsync(item.Id, "  edited text  ", CancellationToken.None);

        Assert.NotNull(updated);
        Assert.Equal("edited text", updated!.TextContent);
        Assert.Equal("edited text", Assert.Single(repository.Items).TextContent);
        Assert.NotEqual(item.Hash, updated.Hash);
    }

    [Fact]
    public async Task UpdateTextContentAsync_rejects_missing_item_and_empty_text()
    {
        var repository = new FakeClipboardRepository();
        var service = CreateService(repository);

        Assert.Null(await service.UpdateTextContentAsync(Guid.NewGuid(), "x", CancellationToken.None));

        await service.CaptureTextAsync(Capture("original"), CancellationToken.None);
        var item = Assert.Single(repository.Items);
        Assert.Null(await service.UpdateTextContentAsync(item.Id, "   ", CancellationToken.None));
        Assert.Equal("original", Assert.Single(repository.Items).TextContent);
    }

    [Fact]
    public async Task AddSnippetAsync_inserts_favorite_text_item()
    {
        var repository = new FakeClipboardRepository();
        var service = CreateService(repository);

        var item = await service.AddSnippetAsync("  请查收，谢谢。  ", CancellationToken.None);

        Assert.NotNull(item);
        Assert.True(item!.IsFavorite);
        Assert.Equal("请查收，谢谢。", item.TextContent);
        Assert.Single(repository.Items);
    }

    [Fact]
    public async Task AddSnippetAsync_rejects_blank_and_marks_existing_duplicate_as_favorite()
    {
        var repository = new FakeClipboardRepository();
        var service = CreateService(repository);

        Assert.Null(await service.AddSnippetAsync("   ", CancellationToken.None));

        await service.CaptureTextAsync(Capture("好的，收到。"), CancellationToken.None);
        var captured = Assert.Single(repository.Items);
        Assert.False(captured.IsFavorite);

        var snippet = await service.AddSnippetAsync("好的，收到。", CancellationToken.None);

        Assert.NotNull(snippet);
        Assert.True(snippet!.IsFavorite);
        Assert.Single(repository.Items);
    }
}
