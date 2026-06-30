using GestureClip.Core.Abstractions;
using GestureClip.Core.Clipboard;
using GestureClip.Core.Gestures;
using GestureClip.Core.Settings;
using GestureClip.Features.Gestures;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GestureClip.Tests.Gestures;

public sealed class GestureBuiltInActionExecutorTests
{
    [Theory]
    [InlineData(BuiltInGestureAction.Copy, "Ctrl+C")]
    [InlineData(BuiltInGestureAction.Paste, "Ctrl+V")]
    [InlineData(BuiltInGestureAction.Cut, "Ctrl+X")]
    [InlineData(BuiltInGestureAction.SelectAll, "Ctrl+A")]
    [InlineData(BuiltInGestureAction.Undo, "Ctrl+Z")]
    [InlineData(BuiltInGestureAction.Redo, "Ctrl+Y")]
    [InlineData(BuiltInGestureAction.Enter, "Enter")]
    [InlineData(BuiltInGestureAction.Escape, "Escape")]
    [InlineData(BuiltInGestureAction.Delete, "Delete")]
    [InlineData(BuiltInGestureAction.Backspace, "Backspace")]
    public async Task ExecuteAsync_sends_keyboard_shortcuts(BuiltInGestureAction action, string expected)
    {
        var keyboard = new FakeKeyboardInputSender();
        var executor = CreateExecutor(keyboard);

        await executor.ExecuteAsync(action, CancellationToken.None);

        Assert.Equal([expected], keyboard.Sent);
    }

    private static GestureBuiltInActionExecutor CreateExecutor(FakeKeyboardInputSender keyboard)
    {
        return new GestureBuiltInActionExecutor(
            new FakeClipboardOverlayService(),
            new FakeClipboardService(),
            new FakeSettingsService(),
            keyboard,
            NullLogger<GestureBuiltInActionExecutor>.Instance);
    }

    private sealed class FakeKeyboardInputSender : IKeyboardInputSender
    {
        public List<string> Sent { get; } = [];
        public string? LastStatus => Sent.LastOrDefault();
        public void SendShortcut(params ushort[] keys) => Sent.Add(KeyName(keys));
        public void SendKey(ushort key) => Sent.Add(KeyName([key]));

        private static string KeyName(IReadOnlyList<ushort> keys)
        {
            return string.Join("+", keys.Select(key => key switch
            {
                0x11 => "Ctrl",
                0x12 => "Alt",
                0x41 => "A",
                0x43 => "C",
                0x56 => "V",
                0x58 => "X",
                0x59 => "Y",
                0x5A => "Z",
                0x0D => "Enter",
                0x1B => "Escape",
                0x2E => "Delete",
                0x08 => "Backspace",
                0x25 => "Left",
                0x27 => "Right",
                _ => key.ToString()
            }));
        }
    }

    private sealed class FakeClipboardOverlayService : IClipboardOverlayService
    {
        public Task ShowAsync() => Task.CompletedTask;
        public Task RefreshAsync() => Task.CompletedTask;
    }

    private sealed class FakeClipboardService : IClipboardService
    {
        public bool IsCaptureEnabled => true;
        public DateTimeOffset? SuppressCaptureUntil => null;
        public void SuppressCaptureFor(TimeSpan duration) { }
        public Task CaptureTextAsync(ClipboardCapture capture, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<ClipboardItem?> GetLatestAsync(CancellationToken cancellationToken) => Task.FromResult<ClipboardItem?>(null);
        public Task PasteAsync(ClipboardItem item, PasteOptions options, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<IReadOnlyList<ClipboardItem>> SearchAsync(string keyword, int limit, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<ClipboardItem>>([]);
        public Task SetCaptureEnabledAsync(bool enabled, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakeSettingsService : ISettingsService
    {
        public T Get<T>(string key, T defaultValue) => defaultValue;
        public Task SetAsync<T>(string key, T value, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
