using GestureClip.App.ViewModels;
using GestureClip.Core.Clipboard;
using Xunit;

namespace GestureClip.Tests.App;

public sealed partial class ClipboardOverlayViewModelTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Selected_paste_passes_target_window_and_plain_text_option(bool plainText)
    {
        const int targetWindowHandle = 456;
        PasteOptions? captured = null;
        var service = new FakeClipboardService([TextItem("selected")])
        {
            PasteHandler = (_, options, _) =>
            {
                captured = options;
                return Task.CompletedTask;
            }
        };
        var viewModel = new ClipboardOverlayViewModel(service);
        await viewModel.LoadAsync();

        Assert.True(await viewModel.PasteSelectedAsync(plainText: plainText, targetWindow: targetWindowHandle));

        Assert.NotNull(captured);
        Assert.Equal((nint)targetWindowHandle, captured.TargetWindowHandle);
        Assert.Equal(plainText, captured.PlainText);
    }

    [Fact]
    public async Task Indexed_paste_passes_target_window_for_requested_row()
    {
        const int targetWindowHandle = 789;
        var row = TextItem("indexed");
        PasteOptions? captured = null;
        Guid? pastedId = null;
        var service = new FakeClipboardService([row])
        {
            PasteHandler = (item, options, _) =>
            {
                captured = options;
                pastedId = item.Id;
                return Task.CompletedTask;
            }
        };
        var viewModel = new ClipboardOverlayViewModel(service);
        await viewModel.LoadAsync();

        Assert.True(await viewModel.PasteByIndexAsync(0, targetWindowHandle));

        Assert.Equal(row.Id, pastedId);
        Assert.NotNull(captured);
        Assert.Equal((nint)targetWindowHandle, captured.TargetWindowHandle);
    }
}
