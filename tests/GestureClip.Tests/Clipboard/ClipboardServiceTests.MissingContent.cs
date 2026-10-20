using GestureClip.Core.Clipboard;
using Xunit;

namespace GestureClip.Tests.Clipboard;

public sealed partial class ClipboardServiceTests
{
    [Theory]
    [InlineData("text", null)]
    [InlineData("text", "")]
    [InlineData("image/png", null)]
    [InlineData("image/png", "")]
    [InlineData("image/png", " ")]
    public async Task Paste_with_missing_content_throws_without_writing_input_or_recording_usage(
        string contentType, string? content)
    {
        var repository = new FakeClipboardRepository();
        var writer = new FakeClipboardWriter();
        var dashboard = new FakeWorkstationDashboardService();
        var service = CreateService(repository, writer: writer, dashboard: dashboard);
        var item = Item("preview") with { ContentType = contentType, TextContent = content };
        repository.Items.Add(item);
        var revision = service.HistoryRevision;

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.PasteAsync(item, new PasteOptions(false), CancellationToken.None));

        Assert.Contains("内容不存在", error.Message);
        Assert.False(writer.PasteHotkeySent);
        Assert.Null(writer.Text);
        Assert.Null(writer.ImagePngBase64);
        Assert.Null(service.SuppressCaptureUntil);
        Assert.Equal(0, dashboard.PasteCount);
        Assert.Empty(repository.IncrementedIds);
        Assert.Equal(revision, service.HistoryRevision);
    }
}
