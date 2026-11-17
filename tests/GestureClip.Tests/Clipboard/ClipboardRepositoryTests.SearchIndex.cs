using GestureClip.Core.Clipboard;
using GestureClip.Features.Clipboard;
using Xunit;

namespace GestureClip.Tests.Clipboard;

public sealed partial class ClipboardRepositoryTests
{
    [Fact]
    public async Task ReadSearchIndex_keeps_matching_text_without_image_payload_or_thumbnail_fields()
    {
        using var database = await TestDatabase.CreateAsync();
        var repository = new ClipboardRepository(database.ConnectionFactory);
        var text = CreateItem("index-text", "微信完整搜索正文", false, minutesAgo: 1);
        var image = CreateImageItem("index-image", "original-image-base64", new string('A', 200_000), minutesAgo: 2)
            with { OcrText = "invoice 483920", PreviewText = "截图" };
        await repository.InsertAsync(text, CancellationToken.None);
        await repository.InsertAsync(image, CancellationToken.None);

        var entries = await repository.ReadSearchIndexAsync(
            new ClipboardSearchIndexPage(10, 0, ClipboardContentFilter.All), CancellationToken.None);

        Assert.Equal([text.Id, image.Id], entries.Select(entry => entry.Id));
        Assert.Equal(text.TextContent, entries[0].TextContent);
        Assert.True(entries[0].IsText);
        Assert.Null(entries[1].TextContent);
        Assert.False(entries[1].IsText);
        Assert.Equal(image.OcrText, entries[1].OcrText);
        Assert.Equal(image.PreviewText, entries[1].PreviewText);
    }

    [Fact]
    public async Task ReadSearchIndex_filters_before_offset_and_retains_list_order()
    {
        using var database = await TestDatabase.CreateAsync();
        var repository = new ClipboardRepository(database.ConnectionFactory);
        var text = CreateItem("index-non-image", "text", false, minutesAgo: 1);
        var firstImage = CreateImageItem("index-first", "first-full", "first-thumb", minutesAgo: 2);
        var secondImage = CreateImageItem("index-second", "second-full", "second-thumb", minutesAgo: 3);
        foreach (var item in new[] { text, firstImage, secondImage })
        {
            await repository.InsertAsync(item, CancellationToken.None);
        }

        var entries = await repository.ReadSearchIndexAsync(
            new ClipboardSearchIndexPage(1, 1, ClipboardContentFilter.Images), CancellationToken.None);

        Assert.Equal(secondImage.Id, Assert.Single(entries).Id);
    }

    [Fact]
    public async Task GetDisplayItems_preserves_match_order_and_loads_thumbnail_without_full_image()
    {
        using var database = await TestDatabase.CreateAsync();
        var repository = new ClipboardRepository(database.ConnectionFactory);
        var first = CreateImageItem("display-first", "first-full", "first-thumb", minutesAgo: 1);
        var second = CreateImageItem("display-second", "second-full", "second-thumb", minutesAgo: 2);
        await repository.InsertAsync(first, CancellationToken.None);
        await repository.InsertAsync(second, CancellationToken.None);

        var results = await repository.GetDisplayItemsAsync([second.Id, first.Id], CancellationToken.None);

        Assert.Equal([second.Id, first.Id], results.Select(item => item.Id));
        Assert.Equal([second.ThumbnailContent, first.ThumbnailContent], results.Select(item => item.ThumbnailContent));
        Assert.All(results, item => Assert.Null(item.TextContent));
        var original = await repository.GetByIdAsync(first.Id, CancellationToken.None);
        Assert.Equal(first.TextContent, original!.TextContent);
    }

    [Fact]
    public async Task GetDisplayItems_returns_all_matches_across_sql_parameter_batches()
    {
        const int itemCount = 205;
        using var database = await TestDatabase.CreateAsync();
        var repository = new ClipboardRepository(database.ConnectionFactory);
        var items = Enumerable.Range(0, itemCount)
            .Select(index => CreateItem($"batch-{index}", $"text {index}", false, minutesAgo: index)).ToArray();
        foreach (var item in items)
        {
            await repository.InsertAsync(item, CancellationToken.None);
        }
        var ids = items.Select(item => item.Id).Reverse().ToArray();

        var results = await repository.GetDisplayItemsAsync(ids, CancellationToken.None);

        Assert.Equal(ids, results.Select(item => item.Id));
    }
}
