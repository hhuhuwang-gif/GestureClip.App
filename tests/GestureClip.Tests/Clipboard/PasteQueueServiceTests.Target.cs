using GestureClip.Core.SystemInfo;
using Xunit;

namespace GestureClip.Tests.Clipboard;

public sealed partial class PasteQueueServiceTests
{
    [Fact]
    public async Task Queue_paste_keeps_hotkey_target_when_focus_changes_during_async_clipboard_write()
    {
        const int originalWindowHandle = 123;
        const int changedWindowHandle = 456;
        var registrar = new FakeRegistrar();
        var targetService = new FakeTargetService { Target = new(originalWindowHandle, "notepad.exe") };
        using var service = CreateService(registrar, out var clipboard, targetService);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        nint? writtenTarget = null;
        clipboard.PasteHandler = async (_, token) =>
        {
            started.SetResult();
            await release.Task.WaitAsync(token);
            writtenTarget = clipboard.LastPasteOptions?.TargetWindowHandle;
        };
        service.Enqueue([Item("office content")]);

        registrar.RaisePasteQueue();
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        targetService.Target = new PasteTarget(changedWindowHandle, "other.exe");
        release.SetResult();
        await WaitForAsync(() => service.PendingCount == 0);

        Assert.Equal((nint)originalWindowHandle, writtenTarget);
        Assert.Equal(1, targetService.CaptureCount);
    }
}
