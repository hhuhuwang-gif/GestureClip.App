using GestureClip.Core.Clipboard;
using Xunit;

namespace GestureClip.Tests.Clipboard;

public sealed partial class PasteQueueServiceTests
{
    [Fact]
    public void Rejected_initial_enqueue_can_be_retried_after_hotkey_becomes_available()
    {
        var registrar = new FakeRegistrar { RegisterResult = false };
        using var service = CreateService(registrar, out _);
        Assert.False(service.Enqueue([Item("first")]));
        registrar.RegisterResult = true;
        Assert.True(service.Enqueue([Item("second")]));
        Assert.Equal(2, registrar.RegisterCount);
        Assert.Equal(PasteQueueStatus.Ready, service.Snapshot.Status);
    }

    [Fact]
    public async Task Failed_paste_keeps_current_item_until_explicit_retry_succeeds()
    {
        var registrar = new FakeRegistrar();
        using var service = CreateService(registrar, out var clipboard);
        clipboard.PasteHandler = (_, _) => Task.FromException(new InvalidOperationException("paste denied"));
        service.Enqueue([Item("first"), Item("second")]);
        registrar.RaisePasteQueue();
        await WaitForAsync(() => service.Snapshot.Status == PasteQueueStatus.Failed);

        Assert.Equal(2, service.PendingCount);
        Assert.Contains("paste denied", service.Snapshot.ErrorMessage);
        registrar.RaisePasteQueue();
        Assert.Equal(PasteQueueStatus.Failed, service.Snapshot.Status);

        clipboard.PasteHandler = null;
        Assert.True(service.Retry());
        registrar.RaisePasteQueue();
        await WaitForAsync(() => service.PendingCount == 1);
        Assert.Equal("first", clipboard.PastedTexts.Single());
    }

    [Fact]
    public async Task Skipping_failed_item_advances_to_next_without_pasting()
    {
        var registrar = new FakeRegistrar();
        using var service = CreateService(registrar, out var clipboard);
        clipboard.PasteHandler = (_, _) => Task.FromException(new InvalidOperationException("blocked"));
        service.Enqueue([Item("first"), Item("second")]);
        registrar.RaisePasteQueue();
        await WaitForAsync(() => service.Snapshot.Status == PasteQueueStatus.Failed);

        Assert.True(service.Skip());
        Assert.Equal(1, service.PendingCount);
        clipboard.PasteHandler = null;
        registrar.RaisePasteQueue();
        await WaitForAsync(() => service.PendingCount == 0);
        Assert.Equal("second", clipboard.PastedTexts.Single());
    }

    [Fact]
    public async Task Clearing_in_flight_paste_cannot_remove_replacement_queue_item()
    {
        var registrar = new FakeRegistrar();
        using var service = CreateService(registrar, out var clipboard);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finish = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        clipboard.PasteHandler = async (_, _) => { started.SetResult(); await finish.Task; };
        service.Enqueue([Item("old")]);
        registrar.RaisePasteQueue();
        await started.Task.WaitAsync(TimeSpan.FromSeconds(2));

        service.Clear();
        service.Enqueue([Item("replacement")]);
        Assert.Equal(1, registrar.RegisterCount);
        finish.SetResult();
        await WaitForAsync(() => registrar.RegisterCount == 2);
        Assert.Equal(1, service.PendingCount);
        clipboard.PasteHandler = null;
        registrar.RaisePasteQueue();
        await WaitForAsync(() => service.PendingCount == 0);
        Assert.Equal("replacement", clipboard.PastedTexts.Single());
    }

    [Fact]
    public async Task Failed_hotkey_reclaim_retains_next_item_and_reports_failure()
    {
        var registrar = new FakeRegistrar();
        using var service = CreateService(registrar, out _);
        service.Enqueue([Item("first"), Item("second")]);
        registrar.RegisterResult = false;
        registrar.RaisePasteQueue();
        await WaitForAsync(() => service.Snapshot.Status == PasteQueueStatus.Failed);
        Assert.Equal(1, service.PendingCount);
        Assert.Contains("Ctrl+V", service.Snapshot.ErrorMessage);
        registrar.RegisterResult = true;
        Assert.True(service.Retry());
        Assert.Equal(PasteQueueStatus.Ready, service.Snapshot.Status);
    }
}
