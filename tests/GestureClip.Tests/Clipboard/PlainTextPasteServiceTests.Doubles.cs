using GestureClip.Core.Abstractions;
using GestureClip.Core.Clipboard;
using GestureClip.Core.SystemInfo;

namespace GestureClip.Tests.Clipboard;

public sealed partial class PlainTextPasteServiceTests
{
    private sealed class TextReader : IClipboardTextReader
    {
        public string? Text { get; init; } = "<b>hello</b>";
        public string? TryReadText() => Text;
        public string? TryReadImagePngBase64() => throw new NotSupportedException();
    }

    private sealed class RecordingWriter : IClipboardWriter
    {
        public Exception? WriteError { get; init; }
        public Exception? PasteError { get; init; }
        public int PasteCount { get; private set; }
        public string? Text { get; private set; }
        public nint? TargetWindow { get; private set; }

        public Task SetTextAsync(string text, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (WriteError is not null) throw WriteError;
            Text = text;
            return Task.CompletedTask;
        }

        public Task SetImagePngBase64Async(string pngBase64, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task SendPasteHotkeyAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            PasteCount++;
            if (PasteError is not null) throw PasteError;
            return Task.CompletedTask;
        }

        public Task SendPasteHotkeyAsync(nint targetWindow, CancellationToken cancellationToken)
        {
            TargetWindow = targetWindow;
            return SendPasteHotkeyAsync(cancellationToken);
        }
    }

    private sealed class TargetService : IPasteTargetService
    {
        public PasteTarget Target { get; set; } = new(123, "notepad.exe");
        public Func<CancellationToken, Task>? WaitHandler { get; init; }
        public PasteTarget? PreparedTarget { get; private set; }
        public PasteTarget CaptureTarget() => Target;
        public Task WaitUntilReadyAsync(PasteTarget target, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            PreparedTarget = target;
            return WaitHandler?.Invoke(cancellationToken) ?? Task.CompletedTask;
        }
    }

    private sealed class CaptureControl : IClipboardService
    {
        public bool IsCaptureEnabled => true;
        public DateTimeOffset? SuppressCaptureUntil { get; private set; }
        public void SuppressCaptureFor(TimeSpan duration) => SuppressCaptureUntil = DateTimeOffset.UtcNow + duration;
        public Task StartAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task StopAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task SetCaptureEnabledAsync(bool enabled, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task CaptureTextAsync(ClipboardCapture capture, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<ClipboardItem>> SearchAsync(string keyword, int limit, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task<ClipboardItem?> GetLatestAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task PasteAsync(ClipboardItem item, PasteOptions options, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task CopyItemsAsync(IReadOnlyList<ClipboardItem> items, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task<int> DeleteItemsAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task SetPinnedAsync(Guid id, bool isPinned, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task SetFavoriteAsync(Guid id, bool isFavorite, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
