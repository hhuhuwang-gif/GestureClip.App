using GestureClip.App.Services;
using Xunit;

namespace GestureClip.Tests.App;

public sealed class ClipboardPasteCoordinatorTests
{
    [Fact]
    public async Task Repeated_paste_during_a_pending_search_shares_one_delivery()
    {
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var requests = new List<ClipboardPasteRequest>();
        var coordinator = new ClipboardPasteCoordinator(request =>
        {
            requests.Add(request);
            return release.Task;
        });
        var firstRequest = new ClipboardPasteRequest(false, true);

        var first = coordinator.PasteAsync(firstRequest);
        var repeated = coordinator.PasteAsync(new ClipboardPasteRequest(true, false, 1));

        Assert.Same(first, repeated);
        Assert.Equal(firstRequest, Assert.Single(requests));
        release.SetResult();
        await first;

        await coordinator.PasteAsync(new ClipboardPasteRequest(true, false));
        Assert.Equal(2, requests.Count);
    }

    [Fact]
    public async Task Failed_paste_remains_visible_and_a_later_attempt_can_run()
    {
        var attempts = 0;
        var coordinator = new ClipboardPasteCoordinator(_ =>
        {
            attempts++;
            return attempts == 1
                ? Task.FromException(new InvalidOperationException("paste failed"))
                : Task.CompletedTask;
        });

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            coordinator.PasteAsync(new ClipboardPasteRequest(false, false)));
        Assert.Equal("paste failed", error.Message);
        await coordinator.PasteAsync(new ClipboardPasteRequest(false, false));
        Assert.Equal(2, attempts);
    }
}
