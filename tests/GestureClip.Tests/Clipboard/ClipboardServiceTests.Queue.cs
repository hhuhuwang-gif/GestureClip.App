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
    public async Task Clipboard_changed_events_are_processed_serially()
    {
        var repository = new FakeClipboardRepository();
        var firstInsertStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirstInsert = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondInsertStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var insertCount = 0;
        repository.InsertHandler = async (_, cancellationToken) =>
        {
            if (Interlocked.Increment(ref insertCount) == 1)
            {
                firstInsertStarted.SetResult();
                await releaseFirstInsert.Task.WaitAsync(cancellationToken);
                return;
            }

            secondInsertStarted.SetResult();
        };

        var listener = new FakeClipboardListener();
        var reader = new FakeClipboardTextReader();
        reader.TextQueue.Enqueue("first");
        reader.TextQueue.Enqueue("second");
        var service = CreateService(repository, listener: listener, reader: reader);

        await service.StartAsync(CancellationToken.None);
        listener.Raise();
        listener.Raise();
        await firstInsertStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));

        var secondStartedBeforeFirstReleased = await Task.WhenAny(secondInsertStarted.Task, Task.Delay(100)) == secondInsertStarted.Task;
        releaseFirstInsert.SetResult();
        await WaitForAsync(() => repository.Items.Count == 2);

        Assert.False(secondStartedBeforeFirstReleased);
    }

    [Fact]
    public async Task Clipboard_changed_rapid_sequence_a_b_c_all_reach_handling()
    {
        var repository = new FakeClipboardRepository();
        var listener = new FakeClipboardListener();
        var reader = new FakeClipboardTextReader();
        reader.TextQueue.Enqueue("A");
        reader.TextQueue.Enqueue("B");
        reader.TextQueue.Enqueue("C");
        var service = CreateService(repository, listener: listener, reader: reader);

        await service.StartAsync(CancellationToken.None);
        listener.Raise();
        listener.Raise();
        listener.Raise();
        await WaitForAsync(() => repository.Items.Count == 3);

        Assert.Equal(new[] { "A", "B", "C" }, repository.Items.Select(item => item.TextContent).ToArray());
    }

    [Fact]
    public async Task Clipboard_changed_retries_when_first_read_fails_and_saves_on_retry()
    {
        // First read returns nothing (transient clipboard unavailability), the retry
        // succeeds: the copy must still be saved.
        var repository = new FakeClipboardRepository();
        var listener = new FakeClipboardListener();
        var reader = new FakeClipboardTextReader();
        reader.TextQueue.Enqueue(null);
        reader.TextQueue.Enqueue("B");
        var service = CreateService(repository, listener: listener, reader: reader);

        await service.StartAsync(CancellationToken.None);
        listener.Raise();
        await WaitForAsync(() => repository.Items.Count == 1);

        Assert.Equal("B", repository.Items[0].TextContent);
    }

    [Fact]
    public async Task Clipboard_changed_retries_long_enough_for_delayed_rendering()
    {
        var repository = new FakeClipboardRepository();
        var listener = new FakeClipboardListener();
        var reader = new FakeClipboardTextReader();
        reader.TextQueue.Enqueue(null);
        reader.TextQueue.Enqueue(null);
        reader.TextQueue.Enqueue(null);
        reader.TextQueue.Enqueue(null);
        reader.TextQueue.Enqueue("late Office text");
        var service = CreateService(repository, listener: listener, reader: reader);

        await service.StartAsync(CancellationToken.None);
        listener.Raise();
        await WaitForAsync(() => repository.Items.Count == 1);

        Assert.Equal("late Office text", repository.Items[0].TextContent);
    }

    [Fact]
    public async Task Clipboard_changed_retry_exhausts_without_spinning_or_saving()
    {
        var repository = new FakeClipboardRepository();
        var listener = new FakeClipboardListener();
        var reader = new FakeClipboardTextReader();
        reader.TextQueue.Enqueue(null);
        reader.TextQueue.Enqueue(null);
        reader.TextQueue.Enqueue(null);
        var service = CreateService(repository, listener: listener, reader: reader);

        await service.StartAsync(CancellationToken.None);
        listener.Raise();
        await Task.Delay(400);

        Assert.Empty(repository.Items);
    }
}
