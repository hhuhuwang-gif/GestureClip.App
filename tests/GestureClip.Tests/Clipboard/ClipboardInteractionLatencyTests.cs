using System.Reflection;
using GestureClip.Core.Abstractions;
using GestureClip.Core.Clipboard;
using GestureClip.Features.Clipboard;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GestureClip.Tests.Clipboard;

public sealed class ClipboardInteractionLatencyTests
{
    [Fact]
    public async Task Copy_finishes_when_the_writer_has_published_the_text()
    {
        var writer = new ControlledWriter();
        var service = CreateService(writer);
        var copy = service.CopyItemsAsync([Item("text")], CancellationToken.None);
        Assert.False(copy.IsCompleted);

        writer.Publish();
        var completedOnPublication = copy.IsCompletedSuccessfully;
        await copy;

        Assert.True(completedOnPublication);
        Assert.Equal("office text", writer.Text);
    }

    [Theory]
    [InlineData("text")]
    [InlineData("image/png")]
    public async Task Paste_is_sent_as_soon_as_the_writer_publishes_content(string contentType)
    {
        var writer = new ControlledWriter();
        var service = CreateService(writer);
        var paste = service.PasteAsync(Item(contentType), new PasteOptions(true), CancellationToken.None);
        Assert.False(writer.PasteSent);

        writer.Publish();
        var sentOnPublication = writer.PasteSent;
        await paste;

        Assert.True(sentOnPublication);
    }

    private static ClipboardItem Item(string contentType)
    {
        var now = DateTimeOffset.UtcNow;
        return new ClipboardItem(Guid.NewGuid(), contentType, "office text", "office text", "hash", null,
            "Office", "office.exe", false, false, false, 0, now, now, null);
    }

    private static ClipboardService CreateService(IClipboardWriter writer)
    {
        return new ClipboardService(
            Proxy<IClipboardListener>(), Proxy<IClipboardTextReader>(), writer,
            Proxy<IClipboardRepository>((method, _) => method.Name == "IncrementUseCountsAsync"
                ? Task.CompletedTask : throw new InvalidOperationException(method.Name)),
            new ClipboardHashService(), new SensitiveContentDetector(),
            Proxy<IForegroundAppService>(), Proxy<IAppBlacklistService>(),
            Proxy<ISettingsService>((_, arguments) => arguments![1]),
            Proxy<IWorkstationDashboardService>((method, _) => method.Name == "RecordPasteAsync"
                ? Task.CompletedTask : throw new InvalidOperationException(method.Name)),
            NullLogger<ClipboardService>.Instance);
    }

    private static T Proxy<T>(Func<MethodInfo, object?[]?, object?>? invoke = null) where T : class
    {
        var instance = DispatchProxy.Create<T, DependencyProxy>();
        ((DependencyProxy)(object)instance).Handler = invoke;
        return instance;
    }

    public class DependencyProxy : DispatchProxy
    {
        public Func<MethodInfo, object?[]?, object?>? Handler { get; set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            return Handler is null
                ? throw new InvalidOperationException(targetMethod!.Name)
                : Handler(targetMethod!, args);
        }
    }

    private sealed class ControlledWriter : IClipboardWriter
    {
        private readonly TaskCompletionSource _published = new();
        public string? Text { get; private set; }
        public bool PasteSent { get; private set; }

        public void Publish() => _published.SetResult();

        public Task SetTextAsync(string text, CancellationToken cancellationToken)
        {
            Text = text;
            return _published.Task;
        }

        public Task SetImagePngBase64Async(string pngBase64, CancellationToken cancellationToken)
            => _published.Task;

        public Task SendPasteHotkeyAsync(CancellationToken cancellationToken)
        {
            PasteSent = true;
            return Task.CompletedTask;
        }
    }
}
