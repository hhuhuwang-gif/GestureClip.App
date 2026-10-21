using GestureClip.Features.Clipboard;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GestureClip.Tests.Clipboard;

public sealed partial class PlainTextPasteServiceTests
{
    [Fact]
    public async Task Failed_clipboard_write_does_not_paste_previous_clipboard_content()
    {
        var writer = new RecordingWriter { WriteError = new InvalidOperationException("clipboard busy") };
        var service = CreateService(writer);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => service.PastePlainTextAsync());

        Assert.Equal("clipboard busy", error.Message);
        Assert.Equal(0, writer.PasteCount);
    }

    [Fact]
    public async Task Failed_injection_is_reported_without_sending_a_second_paste()
    {
        var writer = new RecordingWriter { PasteError = new InvalidOperationException("input denied") };
        var service = CreateService(writer);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.PastePlainTextAsync());

        Assert.Equal(1, writer.PasteCount);
    }

    [Fact]
    public async Task Completed_clipboard_write_does_not_add_a_settle_delay()
    {
        var writer = new RecordingWriter();
        var service = CreateService(writer);

        var paste = service.PastePlainTextAsync();
        var completedImmediately = paste.IsCompleted;
        await paste;

        Assert.True(completedImmediately);
        Assert.Equal(1, writer.PasteCount);
        Assert.Equal("hello", writer.Text);
    }

    [Fact]
    public async Task Waits_for_target_readiness_before_writing_and_keeps_captured_target()
    {
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var target = new TargetService { WaitHandler = token => ready.Task.WaitAsync(token) };
        var writer = new RecordingWriter();
        var paste = CreateService(writer, target).PastePlainTextAsync();

        Assert.False(paste.IsCompleted);
        Assert.Null(writer.Text);
        Assert.Equal(0, writer.PasteCount);
        target.Target = new(456, "other.exe");
        ready.SetResult();
        await paste;

        Assert.Equal((nint)123, writer.TargetWindow);
        Assert.Equal(target.PreparedTarget?.WindowHandle, writer.TargetWindow);
    }

    [Fact]
    public async Task Readiness_failure_does_not_write_or_inject()
    {
        var target = new TargetService
        {
            WaitHandler = _ => Task.FromException(new InvalidOperationException("focus changed"))
        };
        var writer = new RecordingWriter();

        await Assert.ThrowsAsync<InvalidOperationException>(() => CreateService(writer, target).PastePlainTextAsync());

        Assert.Null(writer.Text);
        Assert.Equal(0, writer.PasteCount);
    }

    [Fact]
    public async Task Cancellation_while_waiting_does_not_rewrite_or_paste()
    {
        using var cancellation = new CancellationTokenSource();
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var target = new TargetService { WaitHandler = token => ready.Task.WaitAsync(token) };
        var writer = new RecordingWriter();
        var paste = CreateService(writer, target).PastePlainTextAsync(cancellation.Token);

        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => paste);

        Assert.Null(writer.Text);
        Assert.Equal(0, writer.PasteCount);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("<b></b>")]
    public async Task Empty_text_preserves_existing_normal_paste_with_target_protection(string? text)
    {
        var writer = new RecordingWriter();
        var service = CreateService(writer, new TargetService(), new TextReader { Text = text });

        await service.PastePlainTextAsync();

        Assert.Null(writer.Text);
        Assert.Equal(1, writer.PasteCount);
        Assert.Equal((nint)123, writer.TargetWindow);
    }

    private static PlainTextPasteService CreateService(
        RecordingWriter writer, TargetService? target = null, TextReader? reader = null) =>
        new(new PlainTextPasteDependencies(new CaptureControl(), reader ?? new TextReader(), writer),
            target ?? new TargetService(), NullLogger<PlainTextPasteService>.Instance);
}
