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
    [InlineData(BuiltInGestureAction.NewTab, "Ctrl+T")]
    [InlineData(BuiltInGestureAction.NextTab, "Ctrl+Tab")]
    [InlineData(BuiltInGestureAction.PreviousTab, "Ctrl+Shift+Tab")]
    [InlineData(BuiltInGestureAction.ReopenClosedTab, "Ctrl+Shift+T")]
    [InlineData(BuiltInGestureAction.Refresh, "F5")]
    [InlineData(BuiltInGestureAction.CloseTab, "Ctrl+W")]
    [InlineData(BuiltInGestureAction.StartMenu, "Win")]
    [InlineData(BuiltInGestureAction.ShowDesktop, "Win+D")]
    [InlineData(BuiltInGestureAction.SwitchApp, "Alt+Tab")]
    [InlineData(BuiltInGestureAction.TaskSwitcher, "Ctrl+Alt+Tab")]
    [InlineData(BuiltInGestureAction.PlayPause, "PlayPause")]
    [InlineData(BuiltInGestureAction.VolumeUp, "VolumeUp")]
    [InlineData(BuiltInGestureAction.VolumeDown, "VolumeDown")]
    [InlineData(BuiltInGestureAction.Mute, "Mute")]
    [InlineData(BuiltInGestureAction.PreviousTrack, "PreviousTrack")]
    [InlineData(BuiltInGestureAction.NextTrack, "NextTrack")]
    [InlineData(BuiltInGestureAction.ZoomIn, "Ctrl+Plus")]
    [InlineData(BuiltInGestureAction.ZoomOut, "Ctrl+Minus")]
    [InlineData(BuiltInGestureAction.ResetZoom, "Ctrl+0")]
    [InlineData(BuiltInGestureAction.Home, "Home")]
    [InlineData(BuiltInGestureAction.End, "End")]
    [InlineData(BuiltInGestureAction.PageUp, "PageUp")]
    [InlineData(BuiltInGestureAction.PageDown, "PageDown")]
    [InlineData(BuiltInGestureAction.Screenshot, "Win+Shift+S")]
    [InlineData(BuiltInGestureAction.NextVirtualDesktop, "Ctrl+Win+Right")]
    [InlineData(BuiltInGestureAction.PreviousVirtualDesktop, "Ctrl+Win+Left")]
    [InlineData(BuiltInGestureAction.FullScreen, "F11")]
    public async Task ExecuteAsync_sends_keyboard_shortcuts(BuiltInGestureAction action, string expected)
    {
        var keyboard = new FakeKeyboardInputSender();
        var executor = CreateExecutor(keyboard);

        await executor.ExecuteAsync(action, CancellationToken.None);

        Assert.Equal([expected], keyboard.Sent);
    }

    [Fact]
    public async Task PasteAndEnter_sends_paste_then_enter()
    {
        var keyboard = new FakeKeyboardInputSender();
        var executor = CreateExecutor(keyboard);

        await executor.ExecuteAsync(BuiltInGestureAction.PasteAndEnter, CancellationToken.None);

        Assert.Equal(["Ctrl+V", "Enter"], keyboard.Sent);
    }

    [Theory]
    [InlineData(BuiltInGestureAction.LeftMouseClick, GestureTriggerButton.Left)]
    [InlineData(BuiltInGestureAction.RightMouseClick, GestureTriggerButton.Right)]
    public async Task ExecuteAsync_synthesizes_mouse_click_actions(BuiltInGestureAction action, GestureTriggerButton expectedButton)
    {
        var mouse = new FakeRightClickSynthesizer();
        var cursor = new FakeCursorPositionProvider();
        var executor = CreateExecutor(new FakeKeyboardInputSender(), mouse, cursor);

        await executor.ExecuteAsync(action, CancellationToken.None);

        Assert.Equal(expectedButton, mouse.Clicks.Single().Button);
        Assert.Equal(321, mouse.Clicks.Single().X);
        Assert.Equal(654, mouse.Clicks.Single().Y);
    }

    private static GestureBuiltInActionExecutor CreateExecutor(
        FakeKeyboardInputSender keyboard,
        FakeRightClickSynthesizer? mouse = null,
        FakeCursorPositionProvider? cursor = null)
    {
        return new GestureBuiltInActionExecutor(
            new FakeClipboardOverlayService(),
            new FakeClipboardService(),
            new FakeSettingsService(),
            keyboard,
            mouse ?? new FakeRightClickSynthesizer(),
            cursor ?? new FakeCursorPositionProvider(),
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
                0x09 => "Tab",
                0x41 => "A",
                0x43 => "C",
                0x44 => "D",
                0x56 => "V",
                0x57 => "W",
                0x58 => "X",
                0x59 => "Y",
                0x5A => "Z",
                0x10 => "Shift",
                0x30 => "0",
                0x0D => "Enter",
                0x1B => "Escape",
                0x2E => "Delete",
                0x08 => "Backspace",
                0x25 => "Left",
                0x27 => "Right",
                0x5B => "Win",
                0x54 => "T",
                0x74 => "F5",
                0x7A => "F11",
                0xAD => "Mute",
                0xAE => "VolumeDown",
                0xAF => "VolumeUp",
                0xB0 => "NextTrack",
                0xB1 => "PreviousTrack",
                0xB3 => "PlayPause",
                0xBB => "Plus",
                0xBD => "Minus",
                0x23 => "End",
                0x24 => "Home",
                0x21 => "PageUp",
                0x22 => "PageDown",
                0x53 => "S",
                _ => key.ToString()
            }));
        }
    }

    private sealed class FakeClipboardOverlayService : IClipboardOverlayService
    {
        public Task ShowAsync() => Task.CompletedTask;
        public Task ToggleAsync() => Task.CompletedTask;
        public Task RefreshAsync() => Task.CompletedTask;
    }

    private sealed class FakeRightClickSynthesizer : IRightClickSynthesizer
    {
        public List<(GestureTriggerButton Button, int X, int Y)> Clicks { get; } = [];

        public void SynthesizeRightClick(int x, int y)
        {
            SynthesizeClick(GestureTriggerButton.Right, x, y);
        }

        public void SynthesizeClick(GestureTriggerButton button, int x, int y)
        {
            Clicks.Add((button, x, y));
        }
    }

    private sealed class FakeCursorPositionProvider : ICursorPositionProvider
    {
        public CursorPosition GetCurrentPosition() => new(321, 654, DateTimeOffset.UtcNow);

        public ScreenBounds GetVirtualScreenBounds() => new(0, 0, 1920, 1080);
    }

    private sealed class FakeClipboardService : IClipboardService
    {
        public bool IsCaptureEnabled => true;
        public DateTimeOffset? SuppressCaptureUntil => null;
        public void SuppressCaptureFor(TimeSpan duration) { }
        public Task CaptureTextAsync(ClipboardCapture capture, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<ClipboardItem?> GetLatestAsync(CancellationToken cancellationToken) => Task.FromResult<ClipboardItem?>(null);
        public Task PasteAsync(ClipboardItem item, PasteOptions options, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task CopyItemsAsync(IReadOnlyList<ClipboardItem> items, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<IReadOnlyList<ClipboardItem>> SearchAsync(string keyword, int limit, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<ClipboardItem>>([]);
        public Task<int> DeleteItemsAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken) => Task.FromResult(ids.Count);
        public Task SetPinnedAsync(Guid id, bool isPinned, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task SetFavoriteAsync(Guid id, bool isFavorite, CancellationToken cancellationToken) => Task.CompletedTask;
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
