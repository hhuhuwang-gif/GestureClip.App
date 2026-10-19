using GestureClip.App.ViewModels;
using GestureClip.Core.Abstractions;
using GestureClip.Core.Clipboard;
using Xunit;

namespace GestureClip.Tests.App;

public sealed partial class ClipboardOverlayViewModelTests
{
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
    public async Task SummaryText_shows_item_count_and_selected_count()
    {
        var viewModel = new ClipboardOverlayViewModel(
            new FakeClipboardService([TextItem("first"), TextItem("second")]),
            TimeSpan.Zero);

        await viewModel.LoadAsync();

        Assert.Equal("共 2 条 · 已选 1 条", viewModel.SummaryText);
        Assert.DoesNotContain("已复制", viewModel.SummaryText);

        viewModel.UpdateSelectedCount(2);

        Assert.Equal("共 2 条 · 已选 2 条", viewModel.SummaryText);
    }

    [Fact]
    public async Task SummaryText_includes_session_copied_count_after_copy()
    {
        var first = TextItem("first");
        var second = TextItem("second");
        var service = new FakeClipboardService([first, second]);
        var viewModel = new ClipboardOverlayViewModel(service, TimeSpan.Zero);
        await viewModel.LoadAsync();

        Assert.True(await viewModel.CopySelectedAsync([first]));

        Assert.Equal("共 2 条 · 已选 1 条 · 已复制 1 条", viewModel.SummaryText);
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
        Assert.Contains("图片已复制", viewModel.StatusText);
        Assert.True(viewModel.Items.Single().IsSessionCopied);
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

}
