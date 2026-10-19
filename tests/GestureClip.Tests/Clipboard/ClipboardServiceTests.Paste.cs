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
    public async Task PasteAsync_suppresses_capture_writes_text_sends_paste_and_updates_usage()
    {
        var repository = new FakeClipboardRepository();
        var writer = new FakeClipboardWriter();
        var dashboard = new FakeWorkstationDashboardService();
        var service = CreateService(repository, writer: writer, dashboard: dashboard);
        var item = Item("hello");

        await service.PasteAsync(item, new PasteOptions(false), CancellationToken.None);

        Assert.Equal("hello", writer.Text);
        Assert.True(writer.PasteHotkeySent);
        Assert.True(service.SuppressCaptureUntil > DateTimeOffset.UtcNow);
        Assert.Equal(1, dashboard.PasteCount);
        await WaitForAsync(() => repository.IncrementedId == item.Id);
        Assert.Equal(item.Id, repository.IncrementedId);
    }

    [Fact]
    public async Task PasteAsync_expands_variables_only_for_favorite_snippets()
    {
        var repository = new FakeClipboardRepository();
        var writer = new FakeClipboardWriter();
        var service = CreateService(repository, writer: writer);

        await service.PasteAsync(
            Item("今天 {date}") with { IsFavorite = true },
            new PasteOptions(false),
            CancellationToken.None);
        Assert.Equal($"今天 {DateTimeOffset.Now.ToLocalTime():yyyy-MM-dd}", writer.Text);

        await service.PasteAsync(Item("普通 {date}"), new PasteOptions(false), CancellationToken.None);
        Assert.Equal("普通 {date}", writer.Text);
    }

    [Fact]
    public async Task PasteAsync_returns_after_clipboard_write_records_paste_count_without_waiting_for_usage_stats()
    {
        var repository = new FakeClipboardRepository();
        var releaseIncrement = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        repository.IncrementUseCountHandler = async (_, cancellationToken) =>
        {
            await releaseIncrement.Task.WaitAsync(cancellationToken);
        };
        var writer = new FakeClipboardWriter();
        var dashboard = new FakeWorkstationDashboardService();
        var service = CreateService(repository, writer: writer, dashboard: dashboard);
        var item = Item("hello");

        var pasteTask = service.PasteAsync(item, new PasteOptions(false), CancellationToken.None);
        await WaitForAsync(() => writer.PasteHotkeySent);

        var completedWithoutRepository = await Task.WhenAny(pasteTask, Task.Delay(100)) == pasteTask;
        releaseIncrement.SetResult();
        await pasteTask;

        Assert.True(completedWithoutRepository);
        Assert.Equal(1, dashboard.PasteCount);
    }

    [Fact]
    public async Task PasteAsync_image_returns_after_clipboard_write_without_waiting_for_usage_stats()
    {
        var repository = new FakeClipboardRepository();
        var releaseIncrement = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        repository.IncrementUseCountHandler = async (_, cancellationToken) =>
        {
            await releaseIncrement.Task.WaitAsync(cancellationToken);
        };
        var writer = new FakeClipboardWriter();
        var service = CreateService(repository, writer: writer);
        var now = DateTimeOffset.UtcNow;
        var image = new ClipboardItem(Guid.NewGuid(), "image/jpeg", "png-base64", "图片", "hash", null, "Test", "test.exe", false, false, false, 0, now, now, null);

        var pasteTask = service.PasteAsync(image, new PasteOptions(false), CancellationToken.None);
        await WaitForAsync(() => writer.PasteHotkeySent);

        var completedWithoutRepository = await Task.WhenAny(pasteTask, Task.Delay(100)) == pasteTask;
        releaseIncrement.SetResult();
        await pasteTask;

        Assert.True(completedWithoutRepository);
        Assert.Equal("png-base64", writer.ImagePngBase64);
    }
}
