using GestureClip.Core.Abstractions;
using GestureClip.Core.Clipboard;
using GestureClip.Core.Hotkeys;
using GestureClip.Features.Clipboard;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GestureClip.Tests.Clipboard;

public sealed class PasteQueueServiceTests
{
    [Fact]
    public void Enqueue_claims_hotkey_and_tracks_count()
    {
        var registrar = new FakeRegistrar();
        var service = CreateService(registrar, out _);

        var accepted = service.Enqueue([Item("a"), Item("b")]);

        Assert.True(accepted);
        Assert.Equal(2, service.PendingCount);
        Assert.Equal(1, registrar.RegisterCount);
    }

    [Fact]
    public void Enqueue_fails_and_empties_queue_when_hotkey_unavailable()
    {
        var registrar = new FakeRegistrar { RegisterResult = false };
        var service = CreateService(registrar, out _);

        var accepted = service.Enqueue([Item("a")]);

        Assert.False(accepted);
        Assert.Equal(0, service.PendingCount);
    }

    [Fact]
    public async Task Hotkey_press_pastes_items_in_order_and_releases_hotkey_when_drained()
    {
        var registrar = new FakeRegistrar();
        var service = CreateService(registrar, out var clipboard);
        service.Enqueue([Item("first"), Item("second")]);

        registrar.RaisePasteQueue();
        await WaitForAsync(() => clipboard.PastedTexts.Count == 1);
        Assert.Equal("first", clipboard.PastedTexts[0]);
        Assert.Equal(1, service.PendingCount);

        await WaitForAsync(() => registrar.RegisterCount == 2);

        registrar.RaisePasteQueue();
        await WaitForAsync(() => clipboard.PastedTexts.Count == 2);
        Assert.Equal("second", clipboard.PastedTexts[1]);
        Assert.Equal(0, service.PendingCount);
        await WaitForAsync(() => registrar.UnregisterCount >= 2);
    }

    [Fact]
    public void Clear_empties_queue_and_releases_hotkey()
    {
        var registrar = new FakeRegistrar();
        var service = CreateService(registrar, out _);
        service.Enqueue([Item("a")]);

        service.Clear();

        Assert.Equal(0, service.PendingCount);
        Assert.Equal(1, registrar.UnregisterCount);
    }

    private static PasteQueueService CreateService(FakeRegistrar registrar, out FakeClipboardService clipboard)
    {
        clipboard = new FakeClipboardService();
        return new PasteQueueService(registrar, clipboard, NullLogger<PasteQueueService>.Instance);
    }

    private static ClipboardItem Item(string text)
    {
        var now = DateTimeOffset.UtcNow;
        return new ClipboardItem(Guid.NewGuid(), "text", text, text, text, null, null, null, false, false, false, 0, now, now, null);
    }

    private static async Task WaitForAsync(Func<bool> condition)
    {
        for (var i = 0; i < 100 && !condition(); i++)
        {
            await Task.Delay(10);
        }

        Assert.True(condition());
    }

    private sealed class FakeRegistrar : IHotkeyRegistrar
    {
        public bool RegisterResult { get; set; } = true;
        public int RegisterCount { get; private set; }
        public int UnregisterCount { get; private set; }

        public event EventHandler? HotkeyPressed;
        public event EventHandler? QuickActionHotkeyPressed;
        public event EventHandler? PastePlainTextHotkeyPressed;
        public event EventHandler? PasteQueueHotkeyPressed;

        public bool RegisterOpenClipboardHotkey(HotkeyDefinition hotkey) => true;
        public void UnregisterOpenClipboardHotkey() { }
        public bool RegisterOpenQuickActionHotkey(HotkeyDefinition hotkey) => true;
        public void UnregisterOpenQuickActionHotkey() { }
        public bool RegisterPastePlainTextHotkey(HotkeyDefinition hotkey) => true;
        public void UnregisterPastePlainTextHotkey() { }

        public bool RegisterPasteQueueHotkey(HotkeyDefinition hotkey)
        {
            RegisterCount++;
            return RegisterResult;
        }

        public void UnregisterPasteQueueHotkey()
        {
            UnregisterCount++;
        }

        public int GetLastError() => 0;

        public void RaisePasteQueue() => PasteQueueHotkeyPressed?.Invoke(this, EventArgs.Empty);

        // Silence "never used" warnings for interface events this fake never raises.
        public void RaiseOthers()
        {
            HotkeyPressed?.Invoke(this, EventArgs.Empty);
            QuickActionHotkeyPressed?.Invoke(this, EventArgs.Empty);
            PastePlainTextHotkeyPressed?.Invoke(this, EventArgs.Empty);
        }
    }

    private sealed class FakeClipboardService : IClipboardService
    {
        public List<string> PastedTexts { get; } = [];

        public bool IsCaptureEnabled => true;
        public DateTimeOffset? SuppressCaptureUntil => null;
        public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task SetCaptureEnabledAsync(bool enabled, CancellationToken cancellationToken) => Task.CompletedTask;
        public void SuppressCaptureFor(TimeSpan duration) { }
        public Task CaptureTextAsync(ClipboardCapture capture, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<IReadOnlyList<ClipboardItem>> SearchAsync(string keyword, int limit, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<ClipboardItem>>([]);
        public Task<ClipboardItem?> GetLatestAsync(CancellationToken cancellationToken) => Task.FromResult<ClipboardItem?>(null);

        public Task PasteAsync(ClipboardItem item, PasteOptions options, CancellationToken cancellationToken)
        {
            PastedTexts.Add(item.TextContent ?? "");
            return Task.CompletedTask;
        }

        public Task CopyItemsAsync(IReadOnlyList<ClipboardItem> items, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<int> DeleteItemsAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken) => Task.FromResult(0);
        public Task SetPinnedAsync(Guid id, bool isPinned, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task SetFavoriteAsync(Guid id, bool isFavorite, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
