using GestureClip.App.ViewModels;
using GestureClip.Core.Clipboard;
using Xunit;

namespace GestureClip.Tests.App;

public sealed partial class ClipboardOverlayViewModelTests
{
    [Fact]
    public async Task Selected_image_lookup_runs_in_background_and_selection_cancels_it()
    {
        var image = ImageItem("pending") with { ThumbnailContent = null };
        var text = TextItem("text");
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var release = new ManualResetEventSlim();
        var service = new FakeClipboardService([image, text])
        {
            GetByIdHandler = (_, token) =>
            {
                using var registration = token.Register(() => cancelled.TrySetResult());
                entered.TrySetResult();
                if (!release.Wait(TimeSpan.FromSeconds(5))) throw new TimeoutException("Image lookup was not released");
                token.ThrowIfCancellationRequested();
                return Task.FromResult<ClipboardItem?>(image with { ThumbnailContent = "late-thumbnail" });
            }
        };
        var viewModel = new ClipboardOverlayViewModel(service);

        viewModel.SelectedItem = image;
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        try
        {
            Assert.Null(viewModel.ErrorMessage);
            viewModel.SelectedItem = text;
            await cancelled.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(text.Id, viewModel.SelectedItem.Id);
            Assert.Null(viewModel.ErrorMessage);
        }
        finally
        {
            release.Set();
        }
    }

    [Fact]
    public async Task Image_preview_reuses_requested_thumbnail_and_invalidates_on_history_revision()
    {
        var image = ImageItem("cached") with { ThumbnailContent = null };
        var service = new FakeClipboardService([image]);
        service.FullItems[image.Id] = image with { ThumbnailContent = "cached-preview" };
        var viewModel = new ClipboardOverlayViewModel(service);
        await viewModel.LoadAsync();
        await WaitForAsync(() => viewModel.SelectedItem?.ThumbnailContent == "cached-preview");

        viewModel.SelectedItem = null;
        viewModel.SelectedItem = image;
        Assert.Single(service.GetByIdRequests);

        service.HistoryRevision++;
        service.FullItems[image.Id] = image with { ThumbnailContent = "updated-preview" };
        viewModel.SelectedItem = null;
        viewModel.SelectedItem = image;
        await WaitForAsync(() => viewModel.SelectedItem?.ThumbnailContent == "updated-preview");

        Assert.Equal(2, service.GetByIdRequests.Count);
    }

    [Fact]
    public async Task Late_failure_from_previous_image_does_not_replace_current_selection_or_error()
    {
        var image = ImageItem("late-error") with { ThumbnailContent = null };
        var failure = new TaskCompletionSource<ClipboardItem?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var service = new FakeClipboardService([image])
        {
            GetByIdHandler = (_, _) =>
            {
                entered.TrySetResult();
                return failure.Task;
            }
        };
        var viewModel = new ClipboardOverlayViewModel(service);
        viewModel.SelectedItem = image;
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var text = TextItem("current");
        viewModel.SelectedItem = text;

        failure.SetException(new InvalidOperationException("old image failed"));
        await Task.Yield();

        Assert.Equal(text.Id, viewModel.SelectedItem.Id);
        Assert.Null(viewModel.ErrorMessage);
    }
}
