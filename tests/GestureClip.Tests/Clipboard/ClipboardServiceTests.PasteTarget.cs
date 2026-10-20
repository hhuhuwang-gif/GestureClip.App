using GestureClip.Core.Clipboard;
using Xunit;

namespace GestureClip.Tests.Clipboard;

public sealed partial class ClipboardServiceTests
{
    [Theory]
    [InlineData("text")]
    [InlineData("image/png")]
    public async Task Paste_passes_explicit_window_handle_to_writer(string contentType)
    {
        const int targetWindowHandle = 321;
        var writer = new FakeClipboardWriter();
        var service = CreateService(new FakeClipboardRepository(), writer: writer);
        var item = Item("stored-content") with { ContentType = contentType };

        await service.PasteAsync(item,
            new PasteOptions(false) { TargetWindowHandle = targetWindowHandle }, CancellationToken.None);

        Assert.True(writer.PasteHotkeySent);
        Assert.Equal((nint)targetWindowHandle, writer.PasteTargetWindow);
    }
}
