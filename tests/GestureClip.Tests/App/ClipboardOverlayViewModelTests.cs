using GestureClip.App.ViewModels;
using GestureClip.Core.Abstractions;
using GestureClip.Core.Clipboard;
using Xunit;

namespace GestureClip.Tests.App;

public sealed class ClipboardOverlayViewModelTests
{
    [Fact]
    public async Task LoadAsync_selects_first_item_and_exposes_text_details()
    {
        var now = new DateTimeOffset(2026, 7, 3, 12, 30, 0, TimeSpan.Zero);
        var item = new ClipboardItem(
            Guid.NewGuid(),
            "text",
            "hello world",
            "hello world",
            "hash",
            "plain",
            "Notepad",
            "notepad.exe",
            false,
            false,
            false,
            3,
            now,
            now,
            now);
        var viewModel = new ClipboardOverlayViewModel(new FakeClipboardService([item]));

        await viewModel.LoadAsync();

        Assert.Same(item, viewModel.SelectedItem);
        Assert.True(viewModel.HasSelectedItem);
        Assert.True(viewModel.IsSelectedText);
        Assert.False(viewModel.IsSelectedImage);
        Assert.Equal("文本", viewModel.SelectedContentTypeText);
        Assert.Equal("notepad.exe", viewModel.SelectedSourceText);
        Assert.Equal("使用 3 次", viewModel.SelectedUseCountText);
    }

    [Fact]
    public void SelectedItem_exposes_image_details()
    {
        var now = DateTimeOffset.UtcNow;
        var pngImage = new ClipboardItem(
            Guid.NewGuid(),
            "image/png",
            "png-base64",
            "图片",
            "hash",
            null,
            "SnippingTool",
            "SnippingTool.exe",
            true,
            false,
            false,
            0,
            now,
            now,
            null);
        var viewModel = new ClipboardOverlayViewModel(new FakeClipboardService([]));

        viewModel.SelectedItem = pngImage;

        Assert.True(viewModel.IsSelectedImage);
        Assert.False(viewModel.IsSelectedText);
        Assert.Equal("图片", viewModel.SelectedContentTypeText);
        Assert.Equal("SnippingTool.exe", viewModel.SelectedSourceText);
        Assert.Equal("还没使用过", viewModel.SelectedUseCountText);
    }

    [Fact]
    public async Task SearchText_debounces_rapid_input_and_searches_last_keyword()
    {
        var service = new FakeClipboardService([]);
        var viewModel = new ClipboardOverlayViewModel(service, TimeSpan.FromMilliseconds(30));

        viewModel.SearchText = "a";
        viewModel.SearchText = "ab";
        viewModel.SearchText = "abc";

        await WaitForAsync(() => service.SearchKeywords.Count == 1);

        Assert.Equal(["abc"], service.SearchKeywords);
        Assert.Equal([50], service.SearchLimits);
    }

    [Fact]
    public async Task LoadAsync_uses_default_limit_50()
    {
        var service = new FakeClipboardService([TextItem("first")]);
        var viewModel = new ClipboardOverlayViewModel(service, TimeSpan.Zero);

        await viewModel.LoadAsync();

        Assert.Equal([50], service.SearchLimits);
    }

    [Fact]
    public async Task LoadAsync_with_1000_text_items_keeps_first_page_to_50()
    {
        var items = Enumerable.Range(0, 1000)
            .Select(index => TextItem($"item {index:0000}"))
            .ToArray();
        var service = new FakeClipboardService(items)
        {
            SearchHandler = (_, limit, offset, _) => Task.FromResult<IReadOnlyList<ClipboardItem>>(items.Take(limit).ToArray())
        };
        var viewModel = new ClipboardOverlayViewModel(service, TimeSpan.Zero);

        await viewModel.LoadAsync();

        Assert.Equal(50, viewModel.Items.Count);
        Assert.Equal([50], service.SearchLimits);
    }

    [Fact]
    public async Task LoadAsync_with_100_image_items_keeps_first_page_to_50()
    {
        var now = DateTimeOffset.UtcNow;
        var items = Enumerable.Range(0, 100)
            .Select(index => new ClipboardItem(
                Guid.NewGuid(),
                "image/png",
                "png-base64",
                $"图片 {index}",
                $"hash-{index}",
                null,
                "Test",
                "test.exe",
                false,
                false,
                false,
                0,
                now.AddSeconds(-index),
                now.AddSeconds(-index),
                null))
            .ToArray();
        var service = new FakeClipboardService(items)
        {
            SearchHandler = (_, limit, offset, _) => Task.FromResult<IReadOnlyList<ClipboardItem>>(items.Take(limit).ToArray())
        };
        var viewModel = new ClipboardOverlayViewModel(service, TimeSpan.Zero);

        await viewModel.LoadAsync();

        Assert.Equal(50, viewModel.Items.Count);
        Assert.Equal([50], service.SearchLimits);
    }


    [Fact]
    public async Task LoadMoreAsync_appends_next_page_without_replacing_existing_items()
    {
        var items = Enumerable.Range(0, 120)
            .Select(index => TextItem($"item {index:000}"))
            .ToArray();
        var service = new FakeClipboardService(items)
        {
            SearchHandler = (_, limit, offset, _) => Task.FromResult<IReadOnlyList<ClipboardItem>>(items.Skip(offset).Take(limit).ToArray())
        };
        var viewModel = new ClipboardOverlayViewModel(service, TimeSpan.Zero);

        await viewModel.LoadAsync();
        await viewModel.LoadMoreAsync();

        Assert.Equal(100, viewModel.Items.Count);
        Assert.Equal([0, 50], service.SearchOffsets);
        Assert.True(viewModel.HasMoreItems);
        Assert.Equal("item 000", viewModel.Items[0].TextContent);
        Assert.Equal("item 099", viewModel.Items[^1].TextContent);
    }

    [Fact]
    public async Task LoadMoreAsync_marks_no_more_items_when_short_page_returns()
    {
        var items = Enumerable.Range(0, 70)
            .Select(index => TextItem($"item {index:000}"))
            .ToArray();
        var service = new FakeClipboardService(items)
        {
            SearchHandler = (_, limit, offset, _) => Task.FromResult<IReadOnlyList<ClipboardItem>>(items.Skip(offset).Take(limit).ToArray())
        };
        var viewModel = new ClipboardOverlayViewModel(service, TimeSpan.Zero);

        await viewModel.LoadAsync();
        await viewModel.LoadMoreAsync();

        Assert.Equal(70, viewModel.Items.Count);
        Assert.False(viewModel.HasMoreItems);
        Assert.Equal([0, 50], service.SearchOffsets);
    }

    [Fact]
    public async Task LoadMoreAsync_keeps_search_keyword_when_loading_next_page()
    {
        var items = Enumerable.Range(0, 75)
            .Select(index => TextItem($"alpha item {index:000}"))
            .ToArray();
        var service = new FakeClipboardService(items)
        {
            SearchHandler = (keyword, limit, offset, _) =>
                Task.FromResult<IReadOnlyList<ClipboardItem>>(
                    items.Where(item => item.TextContent!.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                        .Skip(offset)
                        .Take(limit)
                        .ToArray())
        };
        var viewModel = new ClipboardOverlayViewModel(service, TimeSpan.Zero);

        viewModel.SearchText = "alpha";
        await WaitForAsync(() => viewModel.Items.Count == 50);
        await viewModel.LoadMoreAsync();

        Assert.Equal(["alpha", "alpha"], service.SearchKeywords);
        Assert.Equal([0, 50], service.SearchOffsets);
        Assert.Equal(75, viewModel.Items.Count);
        Assert.False(viewModel.HasMoreItems);
    }

    [Fact]
    public async Task LoadAsync_sets_error_message_when_search_fails()
    {
        var service = new FakeClipboardService([])
        {
            SearchHandler = (_, _, _, _) => throw new InvalidOperationException("database busy")
        };
        var viewModel = new ClipboardOverlayViewModel(service, TimeSpan.Zero);

        await viewModel.LoadAsync();

        Assert.True(viewModel.HasError);
        Assert.Contains("database busy", viewModel.ErrorMessage);
        Assert.Equal("加载失败", viewModel.StatusText);
    }

    [Fact]
    public async Task Older_search_result_does_not_replace_newer_result()
    {
        var oldItem = TextItem("old");
        var newItem = TextItem("new");
        var service = new FakeClipboardService([])
        {
            SearchHandler = async (keyword, _, _, cancellationToken) =>
            {
                if (keyword == "old")
                {
                    await Task.Delay(120, cancellationToken);
                    return [oldItem];
                }

                await Task.Delay(10, cancellationToken);
                return [newItem];
            }
        };
        var viewModel = new ClipboardOverlayViewModel(service, TimeSpan.Zero);

        viewModel.SearchText = "old";
        await Task.Delay(20);
        viewModel.SearchText = "new";

        await WaitForAsync(() => viewModel.Items.Count == 1 && viewModel.Items[0].PreviewText == "new");
        await Task.Delay(160);

        Assert.Single(viewModel.Items);
        Assert.Equal("new", viewModel.Items[0].PreviewText);
    }


    [Fact]
    public async Task Selecting_legacy_image_loads_preview_content_on_demand()
    {
        var now = DateTimeOffset.UtcNow;
        var listImage = new ClipboardItem(
            Guid.NewGuid(),
            "image/png",
            null,
            "图片",
            "hash-image",
            null,
            "Test",
            "test.exe",
            false,
            false,
            false,
            0,
            now,
            now,
            null,
            null);
        var fullImage = listImage with { TextContent = "full-image-base64" };
        var service = new FakeClipboardService([listImage]);
        service.FullItems[listImage.Id] = fullImage;
        var viewModel = new ClipboardOverlayViewModel(service, TimeSpan.Zero);

        await viewModel.LoadAsync();
        await WaitForAsync(() => viewModel.SelectedItem?.ThumbnailContent == "full-image-base64");

        Assert.Equal([listImage.Id], service.GetByIdRequests);
        var selected = Assert.IsType<ClipboardItem>(viewModel.SelectedItem);
        Assert.Null(selected.TextContent);
        Assert.Equal("full-image-base64", selected.ThumbnailContent);
    }

    [Fact]
    public async Task Selecting_large_legacy_image_does_not_push_full_image_into_ui_preview_when_thumbnail_fails()
    {
        var now = DateTimeOffset.UtcNow;
        var listImage = new ClipboardItem(
            Guid.NewGuid(),
            "image/png",
            null,
            "图片",
            "hash-large-image",
            null,
            "Test",
            "test.exe",
            false,
            false,
            false,
            0,
            now,
            now,
            null,
            null);
        var largeInvalidImageBase64 = new string('A', 900_000);
        var fullImage = listImage with { TextContent = largeInvalidImageBase64 };
        var service = new FakeClipboardService([listImage]);
        service.FullItems[listImage.Id] = fullImage;
        var viewModel = new ClipboardOverlayViewModel(service, TimeSpan.Zero);

        await viewModel.LoadAsync();
        await WaitForAsync(() => service.GetByIdRequests.Contains(listImage.Id));
        await Task.Delay(80);

        Assert.Null(viewModel.SelectedItem?.ThumbnailContent);
        Assert.NotEqual(largeInvalidImageBase64, viewModel.SelectedItem?.ThumbnailContent);
    }

    [Fact]
    public async Task DeleteItemsAsync_deletes_selected_items_and_refreshes()
    {
        var first = TextItem("first");
        var second = TextItem("second");
        var service = new FakeClipboardService([first, second]);
        var viewModel = new ClipboardOverlayViewModel(service, TimeSpan.Zero);
        await viewModel.LoadAsync();

        var deleted = await viewModel.DeleteItemsAsync([first]);

        Assert.True(deleted);
        Assert.Equal([first.Id], service.DeletedIds);
        Assert.Equal(["second"], viewModel.Items.Select(item => item.TextContent ?? "").ToArray());
        Assert.Equal("已删除 1 条 · Ctrl+Z 撤销", viewModel.StatusText);
        Assert.True(viewModel.CanUndoDelete);
        Assert.Equal(second.Id, viewModel.SelectedItem?.Id);
    }

    [Fact]
    public async Task UndoLastDeleteAsync_restores_deleted_items()
    {
        var first = TextItem("first");
        var second = TextItem("second");
        var service = new FakeClipboardService([first, second]);
        var viewModel = new ClipboardOverlayViewModel(service, TimeSpan.Zero);
        await viewModel.LoadAsync();

        Assert.True(await viewModel.DeleteItemsAsync([first]));
        Assert.True(viewModel.CanUndoDelete);

        var undone = await viewModel.UndoLastDeleteAsync();

        Assert.True(undone);
        Assert.False(viewModel.CanUndoDelete);
        Assert.Contains(first.Id, viewModel.Items.Select(item => item.Id));
        Assert.Equal("已撤销删除", viewModel.StatusText);
        Assert.Contains(first.Id, service.RestoredIds);
    }

    [Fact]
    public async Task CopySelectedAsPlainTextAsync_cleans_blank_lines()
    {
        var item = TextItem("  hello  \n\n\nworld  \n\n");
        var service = new FakeClipboardService([item]);
        service.FullItems[item.Id] = item;
        var viewModel = new ClipboardOverlayViewModel(service, TimeSpan.Zero);
        await viewModel.LoadAsync();

        var copied = await viewModel.CopySelectedAsPlainTextAsync([item]);

        Assert.True(copied);
        Assert.NotNull(service.LastCopiedItems);
        Assert.Equal("  hello\r\n\r\nworld", service.LastCopiedItems![0].TextContent);
        Assert.Contains("纯文本", viewModel.StatusText);
    }

    [Fact]
    public async Task ResetViewAsync_clears_search_and_filter()
    {
        var item = TextItem("hello world");
        var service = new FakeClipboardService([item]);
        var viewModel = new ClipboardOverlayViewModel(service, TimeSpan.Zero);
        await viewModel.LoadAsync();
        viewModel.SearchText = "hello";
        await WaitForAsync(() => viewModel.Items.Count == 1);
        viewModel.SelectedFilter = ClipboardOverlayFilter.Text;

        await viewModel.ResetViewAsync();

        Assert.Equal("", viewModel.SearchText);
        Assert.Equal(ClipboardOverlayFilter.All, viewModel.SelectedFilter);
        Assert.Equal(item.Id, viewModel.SelectedItem?.Id);
        Assert.Equal("已重置视图", viewModel.StatusText);
    }

    [Fact]
    public async Task CopySelectedAsync_does_not_reload_list_after_copy()
    {
        var first = TextItem("first");
        var second = TextItem("second");
        var service = new FakeClipboardService([first, second]);
        var viewModel = new ClipboardOverlayViewModel(service, TimeSpan.Zero);
        await viewModel.LoadAsync();
        var before = viewModel.Items.Select(item => item.Id).ToArray();
        service.SearchKeywords.Clear();

        var copied = await viewModel.CopySelectedAsync([first]);

        Assert.True(copied);
        Assert.Empty(service.SearchKeywords);
        Assert.Equal(before, viewModel.Items.Select(item => item.Id).ToArray());
        Assert.Contains("文字已复制到系统剪贴板", viewModel.StatusText);
    }

    [Fact]
    public async Task CopySelectedAsync_handles_20_fast_clicks_without_reloading()
    {
        var items = Enumerable.Range(0, 50).Select(index => TextItem($"item {index}")).ToArray();
        var service = new FakeClipboardService(items);
        var viewModel = new ClipboardOverlayViewModel(service, TimeSpan.Zero);
        await viewModel.LoadAsync();
        service.SearchKeywords.Clear();

        foreach (var item in viewModel.Items.Take(20).ToArray())
        {
            Assert.True(await viewModel.CopySelectedAsync([item]));
        }

        Assert.Empty(service.SearchKeywords);
        Assert.Equal(20, service.CopyCount);
        Assert.Contains("文字已复制到系统剪贴板", viewModel.StatusText);
    }

    [Fact]
    public async Task TogglePinnedAsync_updates_selected_item_and_refreshes()
    {
        var item = TextItem("pin me");
        var service = new FakeClipboardService([item]);
        var viewModel = new ClipboardOverlayViewModel(service, TimeSpan.Zero);
        await viewModel.LoadAsync();

        var updated = await viewModel.ToggleSelectedPinnedAsync();

        Assert.True(updated);
        Assert.Equal((item.Id, true), service.PinnedUpdates.Single());
        Assert.True(viewModel.Items.Single().IsPinned);
        Assert.Equal("已置顶", viewModel.StatusText);
    }


    [Fact]
    public async Task CopySelectedAsync_handles_100_fast_clicks_without_reloading_or_growing_items()
    {
        var item = TextItem("repeat");
        var service = new FakeClipboardService([item]);
        var viewModel = new ClipboardOverlayViewModel(service, TimeSpan.Zero);
        await viewModel.LoadAsync();
        service.SearchKeywords.Clear();

        for (var index = 0; index < 100; index++)
        {
            Assert.True(await viewModel.CopySelectedAsync([item]));
        }

        Assert.Equal(100, service.CopyCount);
        Assert.Empty(service.SearchKeywords);
        Assert.Single(viewModel.Items);
        Assert.Equal(100, viewModel.Items[0].UseCount);
        Assert.Contains("文字已复制到系统剪贴板", viewModel.StatusText);
    }

    [Fact]
    public async Task ToggleFavoriteAsync_updates_selected_item_and_refreshes()
    {
        var item = TextItem("template");
        var service = new FakeClipboardService([item]);
        var viewModel = new ClipboardOverlayViewModel(service, TimeSpan.Zero);
        await viewModel.LoadAsync();

        var updated = await viewModel.ToggleSelectedFavoriteAsync();

        Assert.True(updated);
        Assert.Equal((item.Id, true), service.FavoriteUpdates.Single());
        Assert.True(viewModel.Items.Single().IsFavorite);
        Assert.Equal("已保存为片段", viewModel.StatusText);
    }

    [Fact]
    public async Task Filter_shows_only_matching_clipboard_items()
    {
        var pinnedText = TextItem("pinned") with { IsPinned = true };
        var favoriteText = TextItem("favorite") with { IsFavorite = true };
        var normalText = TextItem("normal");
        var pngImage = new ClipboardItem(
            Guid.NewGuid(),
            "image/png",
            "png-base64",
            "图片",
            "image-hash",
            null,
            "SnippingTool",
            "snippingtool.exe",
            false,
            false,
            false,
            0,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            null);
        var jpegImage = ImageItem("JPEG 图片", "image/jpeg");
        var viewModel = new ClipboardOverlayViewModel(new FakeClipboardService([pinnedText, favoriteText, normalText, pngImage, jpegImage]), TimeSpan.Zero);
        await viewModel.LoadAsync();

        viewModel.SelectedFilter = ClipboardOverlayFilter.Pinned;
        await WaitForAsync(() => viewModel.Items.Count == 1 && viewModel.Items.FirstOrDefault()?.PreviewText == "pinned");
        Assert.Equal(["pinned"], viewModel.Items.Select(item => item.PreviewText ?? "").ToArray());

        viewModel.SelectedFilter = ClipboardOverlayFilter.Favorites;
        await WaitForAsync(() => viewModel.Items.Count == 1 && viewModel.Items.FirstOrDefault()?.PreviewText == "favorite");
        Assert.Equal(["favorite"], viewModel.Items.Select(item => item.PreviewText ?? "").ToArray());

        viewModel.SelectedFilter = ClipboardOverlayFilter.Images;
        await WaitForAsync(() => viewModel.Items.Count == 2);
        Assert.Equal(["JPEG 图片", "图片"], viewModel.Items.Select(item => item.PreviewText ?? "").ToArray());

        viewModel.SelectedFilter = ClipboardOverlayFilter.Text;
        await WaitForAsync(() => viewModel.Items.Count == 3 && viewModel.Items.All(item => item.ContentType == "text"));
        Assert.Equal(["pinned", "normal", "favorite"], viewModel.Items.Select(item => item.PreviewText ?? "").ToArray());

        viewModel.SelectedFilter = ClipboardOverlayFilter.All;
        await WaitForAsync(() => viewModel.Items.Count == 5);
        Assert.Equal(["pinned", "JPEG 图片", "图片", "normal", "favorite"], viewModel.Items.Select(item => item.PreviewText ?? "").ToArray());
    }

    [Fact]
    public async Task SelectedFilter_queries_database_filter_instead_of_current_page()
    {
        var textItems = Enumerable.Range(0, 50)
            .Select(index => TextItem($"text {index:000}"))
            .ToArray();
        var image = ImageItem("图片 001");
        var service = new FakeClipboardService(textItems)
        {
            SearchHandlerWithFilter = (_, _, _, filter, _) =>
            {
                return Task.FromResult<IReadOnlyList<ClipboardItem>>(
                    filter == ClipboardContentFilter.Images ? [image] : textItems);
            }
        };
        var viewModel = new ClipboardOverlayViewModel(service, TimeSpan.Zero);

        await viewModel.LoadAsync();
        viewModel.SelectedFilter = ClipboardOverlayFilter.Images;
        await WaitForAsync(() => service.SearchFilters.Contains(ClipboardContentFilter.Images));

        Assert.Equal([ClipboardContentFilter.All, ClipboardContentFilter.Images], service.SearchFilters);
        Assert.Equal(["图片 001"], viewModel.Items.Select(item => item.PreviewText ?? "").ToArray());
    }

    [Fact]
    public async Task LoadMoreAsync_uses_selected_database_filter()
    {
        var firstPage = Enumerable.Range(0, 50)
            .Select(index => ImageItem($"图片 {index:000}"))
            .ToArray();
        var secondPage = Enumerable.Range(50, 10)
            .Select(index => ImageItem($"图片 {index:000}"))
            .ToArray();
        var service = new FakeClipboardService(firstPage.Concat(secondPage).ToArray())
        {
            SearchHandlerWithFilter = (_, limit, offset, filter, _) =>
            {
                Assert.Equal(ClipboardContentFilter.Images, filter);
                return Task.FromResult<IReadOnlyList<ClipboardItem>>(
                    offset == 0 ? firstPage.Take(limit).ToArray() : secondPage.Take(limit).ToArray());
            }
        };
        var viewModel = new ClipboardOverlayViewModel(service, TimeSpan.Zero);

        viewModel.SelectedFilter = ClipboardOverlayFilter.Images;
        await WaitForAsync(() => viewModel.Items.Count == 50);
        await viewModel.LoadMoreAsync();

        Assert.Equal([0, 50], service.SearchOffsets);
        Assert.Equal([ClipboardContentFilter.Images, ClipboardContentFilter.Images], service.SearchFilters);
        Assert.Equal(60, viewModel.Items.Count);
    }

    [Fact]
    public async Task ClearSearchAsync_clears_search_text_and_refreshes_results()
    {
        var service = new FakeClipboardService([TextItem("alpha"), TextItem("beta")]);
        var viewModel = new ClipboardOverlayViewModel(service, TimeSpan.Zero);
        viewModel.SearchText = "alpha";
        await WaitForAsync(() => service.SearchKeywords.Contains("alpha"));

        var cleared = await viewModel.ClearSearchAsync();

        Assert.True(cleared);
        Assert.Equal("", viewModel.SearchText);
        Assert.Contains("", service.SearchKeywords);
    }

    [Fact]
    public async Task ClearSearchAsync_restores_recent_history_first_page_without_full_reload()
    {
        var items = Enumerable.Range(0, 1000)
            .Select(index => TextItem($"item {index:0000}"))
            .ToArray();
        var service = new FakeClipboardService(items);
        var viewModel = new ClipboardOverlayViewModel(service, TimeSpan.Zero);
        viewModel.SearchText = "item 0001";
        await WaitForAsync(() => service.SearchKeywords.Contains("item 0001"));

        await viewModel.ClearSearchAsync();

        Assert.Equal("", viewModel.SearchText);
        Assert.Equal(50, viewModel.Items.Count);
        Assert.Equal(50, service.SearchLimits.Last());
        Assert.Equal(0, service.SearchOffsets.Last());
    }

    [Fact]
    public async Task SummaryText_shows_item_count_and_selected_count()
    {
        var viewModel = new ClipboardOverlayViewModel(
            new FakeClipboardService([TextItem("first"), TextItem("second")]),
            TimeSpan.Zero);

        await viewModel.LoadAsync();

        Assert.Equal("共 2 条 · 已选 1 条", viewModel.SummaryText);

        viewModel.UpdateSelectedCount(2);

        Assert.Equal("共 2 条 · 已选 2 条", viewModel.SummaryText);
    }

    [Fact]
    public async Task PasteSelectedAsync_shows_error_without_throwing_when_clipboard_write_fails()
    {
        var item = TextItem("paste me");
        var service = new FakeClipboardService([item])
        {
            PasteHandler = (_, _, _) => throw new InvalidOperationException("clipboard busy")
        };
        var viewModel = new ClipboardOverlayViewModel(service, TimeSpan.Zero);
        await viewModel.LoadAsync();

        var pasted = await viewModel.PasteSelectedAsync();

        Assert.False(pasted);
        Assert.True(viewModel.HasError);
        Assert.Contains("clipboard busy", viewModel.ErrorMessage);
        Assert.Equal("粘贴失败", viewModel.StatusText);
    }

    [Fact]
    public async Task CopySelectedAsync_shows_error_without_throwing_when_clipboard_write_fails()
    {
        var item = TextItem("copy me");
        var service = new FakeClipboardService([item])
        {
            CopyHandler = (_, _) => throw new InvalidOperationException("clipboard busy")
        };
        var viewModel = new ClipboardOverlayViewModel(service, TimeSpan.Zero);
        await viewModel.LoadAsync();

        var copied = await viewModel.CopySelectedAsync([item]);

        Assert.False(copied);
        Assert.True(viewModel.HasError);
        Assert.Contains("clipboard busy", viewModel.ErrorMessage);
        Assert.Equal("复制失败", viewModel.StatusText);
    }

    [Fact]
    public async Task CopySelectedAsync_shows_clear_status_for_image_copy()
    {
        var image = ImageItem("图片");
        var service = new FakeClipboardService([image]);
        var viewModel = new ClipboardOverlayViewModel(service, TimeSpan.Zero);
        await viewModel.LoadAsync();

        var copied = await viewModel.CopySelectedAsync([image]);

        Assert.True(copied);
        Assert.Contains("图片已复制到系统剪贴板", viewModel.StatusText);
    }

    [Fact]
    public async Task CopySelectedAsync_shows_image_error_message_when_image_copy_fails()
    {
        var image = ImageItem("图片");
        var service = new FakeClipboardService([image])
        {
            CopyHandler = (_, _) => throw new InvalidOperationException("image clipboard busy")
        };
        var viewModel = new ClipboardOverlayViewModel(service, TimeSpan.Zero);
        await viewModel.LoadAsync();

        var copied = await viewModel.CopySelectedAsync([image]);

        Assert.False(copied);
        Assert.True(viewModel.HasError);
        Assert.Contains("image clipboard busy", viewModel.ErrorMessage);
        Assert.Equal("图片复制失败：image clipboard busy", viewModel.StatusText);
    }

    private static ClipboardItem TextItem(string text)
    {
        var now = DateTimeOffset.UtcNow;
        return new ClipboardItem(
            Guid.NewGuid(),
            "text",
            text,
            text,
            text,
            text,
            "Test",
            "test.exe",
            false,
            false,
            false,
            0,
            now,
            now,
            null);
    }

    private static ClipboardItem ImageItem(string previewText, string contentType = "image/png")
    {
        var now = DateTimeOffset.UtcNow;
        return new ClipboardItem(
            Guid.NewGuid(),
            contentType,
            null,
            previewText,
            $"hash-{previewText}",
            null,
            "Test",
            "test.exe",
            false,
            false,
            false,
            0,
            now,
            now,
            null,
            $"thumb-{previewText}");
    }

    private static async Task WaitForAsync(Func<bool> condition)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(2);
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(10);
        }

        Assert.True(condition());
    }

    private sealed class FakeClipboardService : IClipboardService
    {
        private readonly IReadOnlyList<ClipboardItem> _items;

        public FakeClipboardService(IReadOnlyList<ClipboardItem> items)
        {
            _items = items;
        }

        public bool IsCaptureEnabled => true;

        public List<string> SearchKeywords { get; } = [];

        public List<int> SearchLimits { get; } = [];

        public List<int> SearchOffsets { get; } = [];

        public List<ClipboardContentFilter> SearchFilters { get; } = [];

        public List<Guid> DeletedIds { get; } = [];

        public List<Guid> RestoredIds { get; } = [];

        public Dictionary<Guid, ClipboardItem> FullItems { get; } = [];

        public List<Guid> GetByIdRequests { get; } = [];

        public List<(Guid Id, bool IsPinned)> PinnedUpdates { get; } = [];

        public List<(Guid Id, bool IsFavorite)> FavoriteUpdates { get; } = [];

        public int CopyCount { get; private set; }

        public IReadOnlyList<ClipboardItem>? LastCopiedItems { get; private set; }

        public Func<string, int, int, CancellationToken, Task<IReadOnlyList<ClipboardItem>>>? SearchHandler { get; set; }

        public Func<string, int, int, ClipboardContentFilter, CancellationToken, Task<IReadOnlyList<ClipboardItem>>>? SearchHandlerWithFilter { get; set; }

        public Func<ClipboardItem, PasteOptions, CancellationToken, Task>? PasteHandler { get; set; }

        public Func<IReadOnlyList<ClipboardItem>, CancellationToken, Task>? CopyHandler { get; set; }

        public DateTimeOffset? SuppressCaptureUntil => null;

        public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task SetCaptureEnabledAsync(bool enabled, CancellationToken cancellationToken) => Task.CompletedTask;

        public void SuppressCaptureFor(TimeSpan duration) { }

        public Task CaptureTextAsync(ClipboardCapture capture, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<IReadOnlyList<ClipboardItem>> SearchAsync(string keyword, int limit, CancellationToken cancellationToken)
        {
            return SearchAsync(keyword, limit, 0, cancellationToken);
        }

        public Task<IReadOnlyList<ClipboardItem>> SearchAsync(string keyword, int limit, int offset, CancellationToken cancellationToken)
        {
            return SearchAsync(keyword, limit, offset, ClipboardContentFilter.All, cancellationToken);
        }

        public Task<IReadOnlyList<ClipboardItem>> SearchAsync(string keyword, int limit, int offset, ClipboardContentFilter filter, CancellationToken cancellationToken)
        {
            SearchKeywords.Add(keyword);
            SearchLimits.Add(limit);
            SearchOffsets.Add(offset);
            SearchFilters.Add(filter);
            var visibleItems = _items.Where(item => !DeletedIds.Contains(item.Id))
                .Select(item =>
                {
                    var pinnedUpdate = PinnedUpdates.LastOrDefault(update => update.Id == item.Id);
                    var favoriteUpdate = FavoriteUpdates.LastOrDefault(update => update.Id == item.Id);
                    var updated = pinnedUpdate.Id == item.Id
                        ? item with { IsPinned = pinnedUpdate.IsPinned }
                        : item;
                    return favoriteUpdate.Id == item.Id
                        ? updated with { IsFavorite = favoriteUpdate.IsFavorite }
                        : updated;
                })
                .OrderByDescending(item => item.IsPinned)
                .ThenByDescending(item => item.CreatedAt)
                .ToArray();

            if (SearchHandlerWithFilter is not null)
            {
                return SearchHandlerWithFilter(keyword, limit, offset, filter, cancellationToken);
            }

            return SearchHandler is null
                ? Task.FromResult<IReadOnlyList<ClipboardItem>>(visibleItems.Where(item => MatchesFilter(item, filter)).Skip(offset).Take(limit).ToArray())
                : SearchHandler(keyword, limit, offset, cancellationToken);
        }

        private static bool MatchesFilter(ClipboardItem item, ClipboardContentFilter filter)
        {
            return filter switch
            {
                ClipboardContentFilter.Pinned => item.IsPinned,
                ClipboardContentFilter.Favorites => item.IsFavorite,
                ClipboardContentFilter.Text => item.IsText,
                ClipboardContentFilter.Images => item.IsImage,
                _ => true
            };
        }

        public Task<ClipboardItem?> GetLatestAsync(CancellationToken cancellationToken) => Task.FromResult<ClipboardItem?>(null);

        public Task<ClipboardItem?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
        {
            GetByIdRequests.Add(id);
            return Task.FromResult(FullItems.TryGetValue(id, out var item) ? item : null);
        }

        public Task PasteAsync(ClipboardItem item, PasteOptions options, CancellationToken cancellationToken)
        {
            return PasteHandler is null
                ? Task.CompletedTask
                : PasteHandler(item, options, cancellationToken);
        }

        public Task CopyItemsAsync(IReadOnlyList<ClipboardItem> items, CancellationToken cancellationToken)
        {
            CopyCount++;
            LastCopiedItems = items.ToArray();
            return CopyHandler is null
                ? Task.CompletedTask
                : CopyHandler(items, cancellationToken);
        }

        public Task<int> DeleteItemsAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken)
        {
            DeletedIds.AddRange(ids);
            return Task.FromResult(ids.Count);
        }

        public Task RestoreItemsAsync(IReadOnlyList<ClipboardItem> items, CancellationToken cancellationToken)
        {
            foreach (var item in items)
            {
                RestoredIds.Add(item.Id);
                DeletedIds.RemoveAll(id => id == item.Id);
            }

            return Task.CompletedTask;
        }

        public Task SetPinnedAsync(Guid id, bool isPinned, CancellationToken cancellationToken)
        {
            PinnedUpdates.Add((id, isPinned));
            return Task.CompletedTask;
        }

        public Task SetFavoriteAsync(Guid id, bool isFavorite, CancellationToken cancellationToken)
        {
            FavoriteUpdates.Add((id, isFavorite));
            return Task.CompletedTask;
        }
    }
}
