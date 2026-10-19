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
    public async Task CopyItemsAsync_merges_multiple_text_items_with_new_lines()
    {
        var repository = new FakeClipboardRepository();
        var writer = new FakeClipboardWriter();
        var service = CreateService(repository, writer: writer);
        var first = Item("hello");
        var second = Item("world");

        await service.CopyItemsAsync([first, second], CancellationToken.None);

        Assert.Equal("hello\r\nworld", writer.Text);
        await WaitForAsync(() => repository.IncrementedIds.Count == 2);
        Assert.Equal([first.Id, second.Id], repository.IncrementedIds);
    }

    [Fact]
    public async Task CopyItemsAsync_copies_large_text_without_truncating_or_waiting_for_usage_updates()
    {
        var repository = new FakeClipboardRepository();
        var releaseIncrement = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        repository.IncrementUseCountHandler = async (_, cancellationToken) =>
        {
            await releaseIncrement.Task.WaitAsync(cancellationToken);
        };
        var writer = new FakeClipboardWriter();
        var service = CreateService(repository, writer: writer);
        var largeText = string.Concat(Enumerable.Repeat("长文本复制不卡顿。", 12_000));
        var item = Item(largeText);

        var copyTask = service.CopyItemsAsync([item], CancellationToken.None);
        await WaitForAsync(() => writer.Text == largeText);

        var completedWithoutUsage = await Task.WhenAny(copyTask, Task.Delay(200)) == copyTask;
        releaseIncrement.SetResult();
        await copyTask;

        Assert.True(completedWithoutUsage);
        Assert.Equal(largeText.Length, writer.Text!.Length);
    }

    [Fact]
    public async Task CopyItemsAsync_finishes_when_text_has_been_published()
    {
        var repository = new FakeClipboardRepository();
        var writer = new FakeClipboardWriter();
        var service = CreateService(repository, writer: writer);
        var item = Item("ready");

        var copy = service.CopyItemsAsync([item], CancellationToken.None);
        var completedOnPublication = copy.IsCompletedSuccessfully;
        await copy;

        Assert.Equal("ready", writer.Text);
        Assert.True(completedOnPublication);
    }

    [Fact]
    public async Task CopyItemsAsync_suppresses_capture_to_prevent_internal_copy_feedback_loop()
    {
        var repository = new FakeClipboardRepository();
        var writer = new FakeClipboardWriter();
        var service = CreateService(repository, writer: writer);

        await service.CopyItemsAsync([Item("internal copy")], CancellationToken.None);
        await service.CaptureTextAsync(Capture("internal copy"), CancellationToken.None);

        Assert.True(service.SuppressCaptureUntil > DateTimeOffset.UtcNow);
        Assert.Empty(repository.Items);
    }

    [Fact]
    public async Task CopyItemsAsync_returns_after_clipboard_write_without_waiting_for_usage_updates()
    {
        var repository = new FakeClipboardRepository();
        var releaseIncrement = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        repository.IncrementUseCountHandler = async (_, cancellationToken) =>
        {
            await releaseIncrement.Task.WaitAsync(cancellationToken);
        };
        var writer = new FakeClipboardWriter();
        var service = CreateService(repository, writer: writer);
        var first = Item("hello");
        var second = Item("world");

        var copyTask = service.CopyItemsAsync([first, second], CancellationToken.None);
        await WaitForAsync(() => writer.Text == "hello\r\nworld");

        var completedWithoutUsage = await Task.WhenAny(copyTask, Task.Delay(100)) == copyTask;
        releaseIncrement.SetResult();
        await copyTask;

        Assert.True(completedWithoutUsage);
    }

    [Fact]
    public async Task CopyItemsAsync_coalesces_repeated_use_count_updates()
    {
        var repository = new FakeClipboardRepository();
        var writer = new FakeClipboardWriter();
        var service = CreateService(repository, writer: writer);
        var item = Item("hello");

        var copyTasks = Enumerable.Range(0, 20)
            .Select(_ => service.CopyItemsAsync([item], CancellationToken.None))
            .ToArray();

        await Task.WhenAll(copyTasks);
        await WaitForAsync(() => repository.BatchedUseCounts.TryGetValue(item.Id, out var count) && count == 20);

        Assert.Equal(1, repository.BatchIncrementCallCount);
        Assert.Equal(20, repository.BatchedUseCounts[item.Id]);
    }

    [Fact]
    public async Task StopAsync_flushes_pending_use_count_updates()
    {
        var repository = new FakeClipboardRepository();
        var listener = new FakeClipboardListener();
        var writer = new FakeClipboardWriter();
        var service = CreateService(repository, listener: listener, writer: writer);
        var item = Item("hello");
        await service.StartAsync(CancellationToken.None);

        await service.CopyItemsAsync([item], CancellationToken.None);
        await service.StopAsync(CancellationToken.None);

        Assert.Equal(1, repository.BatchedUseCounts[item.Id]);
    }

    [Fact]
    public async Task CopyItemsAsync_restores_single_image_to_clipboard()
    {
        var repository = new FakeClipboardRepository();
        var writer = new FakeClipboardWriter();
        var service = CreateService(repository, writer: writer);
        var now = DateTimeOffset.UtcNow;
        var image = new ClipboardItem(Guid.NewGuid(), "image/jpeg", "png-base64", "图片", "hash", null, "Test", "test.exe", false, false, false, 0, now, now, null);

        await service.CopyItemsAsync([image], CancellationToken.None);

        Assert.Equal("png-base64", writer.ImagePngBase64);
        await WaitForAsync(() => repository.IncrementedIds.Count == 1);
        Assert.Equal([image.Id], repository.IncrementedIds);
    }

    [Fact]
    public async Task CopyItemsAsync_loads_full_image_content_when_list_item_only_has_thumbnail()
    {
        var repository = new FakeClipboardRepository();
        var writer = new FakeClipboardWriter();
        var service = CreateService(repository, writer: writer);
        var now = DateTimeOffset.UtcNow;
        var full = new ClipboardItem(Guid.NewGuid(), "image/png", "full-image-base64", "图片", "hash", null, "Test", "test.exe", false, false, false, 0, now, now, null, "thumb-base64");
        var listItem = full with { TextContent = null };
        repository.Items.Add(full);

        await service.CopyItemsAsync([listItem], CancellationToken.None);

        Assert.Equal("full-image-base64", writer.ImagePngBase64);
    }

    [Fact]
    public async Task CopyItemsAsync_rejects_mixed_text_and_image_items()
    {
        var repository = new FakeClipboardRepository();
        var service = CreateService(repository);
        var now = DateTimeOffset.UtcNow;
        var image = new ClipboardItem(Guid.NewGuid(), "image/png", "png-base64", "图片", "hash", null, "Test", "test.exe", false, false, false, 0, now, now, null);

        await Assert.ThrowsAsync<NotSupportedException>(() => service.CopyItemsAsync([Item("hello"), image], CancellationToken.None));
    }


    [Fact]
    public async Task CopyItemsAsync_strips_html_tags_when_rewriting_system_clipboard()
    {
        var repository = new FakeClipboardRepository();
        var writer = new FakeClipboardWriter();
        var service = CreateService(repository, writer: writer);
        var item = Item("<p>Hello <b>world</b></p>");

        await service.CopyItemsAsync([item], CancellationToken.None);

        Assert.NotNull(writer.Text);
        Assert.DoesNotContain("<p>", writer.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("<b>", writer.Text, StringComparison.Ordinal);
        Assert.Contains("Hello", writer.Text, StringComparison.Ordinal);
        Assert.Contains("world", writer.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CopyItemsAsync_keeps_plain_code_newlines_intact_when_no_html()
    {
        var repository = new FakeClipboardRepository();
        var writer = new FakeClipboardWriter();
        var service = CreateService(repository, writer: writer);
        var code = "public void M()\r\n{\r\n    Console.WriteLine(\"x\");\r\n}";
        var item = Item(code);

        await service.CopyItemsAsync([item], CancellationToken.None);

        Assert.Equal(code, writer.Text);
    }
}
