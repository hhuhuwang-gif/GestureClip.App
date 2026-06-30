using GestureClip.Core.Abstractions;
using GestureClip.Core.Clipboard;
using GestureClip.Core.SystemInfo;
using GestureClip.Features.Clipboard;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GestureClip.Tests.Clipboard;

public sealed class ClipboardServiceTests
{
    [Fact]
    public async Task CaptureTextAsync_skips_when_capture_is_disabled()
    {
        var repository = new FakeClipboardRepository();
        var service = CreateService(repository);
        await service.SetCaptureEnabledAsync(false, CancellationToken.None);

        await service.CaptureTextAsync(Capture("hello"), CancellationToken.None);

        Assert.Empty(repository.Items);
    }

    [Fact]
    public async Task CaptureTextAsync_skips_empty_blacklisted_duplicate_and_sensitive_text()
    {
        var repository = new FakeClipboardRepository { BlockedProcessName = "vault.exe" };
        var service = CreateService(repository);

        await service.CaptureTextAsync(Capture("   "), CancellationToken.None);
        await service.CaptureTextAsync(Capture("secret", process: "vault.exe"), CancellationToken.None);
        await service.CaptureTextAsync(Capture("normal"), CancellationToken.None);
        await service.CaptureTextAsync(Capture("normal"), CancellationToken.None);
        await service.CaptureTextAsync(Capture("123456"), CancellationToken.None);

        Assert.Single(repository.Items);
        Assert.Equal("normal", repository.Items[0].TextContent);
    }

    [Fact]
    public async Task PasteAsync_suppresses_capture_writes_text_sends_paste_and_updates_usage()
    {
        var repository = new FakeClipboardRepository();
        var writer = new FakeClipboardWriter();
        var service = CreateService(repository, writer: writer);
        var item = Item("hello");

        await service.PasteAsync(item, new PasteOptions(false), CancellationToken.None);

        Assert.Equal("hello", writer.Text);
        Assert.True(writer.PasteHotkeySent);
        Assert.True(service.SuppressCaptureUntil > DateTimeOffset.UtcNow);
        Assert.Equal(item.Id, repository.IncrementedId);
    }

    [Fact]
    public async Task StartAsync_and_StopAsync_are_idempotent()
    {
        var repository = new FakeClipboardRepository();
        var listener = new FakeClipboardListener();
        var service = CreateService(repository, listener: listener);

        await service.StartAsync(CancellationToken.None);
        await service.StartAsync(CancellationToken.None);
        await service.StopAsync(CancellationToken.None);
        await service.StopAsync(CancellationToken.None);

        Assert.Equal(1, listener.StartCount);
        Assert.Equal(1, listener.StopCount);
    }

    private static ClipboardService CreateService(
        FakeClipboardRepository repository,
        FakeClipboardWriter? writer = null,
        FakeClipboardListener? listener = null)
    {
        return new ClipboardService(
            listener ?? new FakeClipboardListener(),
            new FakeClipboardTextReader(),
            writer ?? new FakeClipboardWriter(),
            repository,
            new ClipboardHashService(),
            new SensitiveContentDetector(),
            new FakeForegroundAppService(),
            new FakeAppBlacklistService(repository),
            new FakeSettingsService(),
            NullLogger<ClipboardService>.Instance);
    }

    private static ClipboardCapture Capture(string text, string process = "notepad.exe")
    {
        return new ClipboardCapture(text, "Test", process, DateTimeOffset.UtcNow);
    }

    private static ClipboardItem Item(string text)
    {
        var now = DateTimeOffset.UtcNow;
        return new ClipboardItem(Guid.NewGuid(), "text", text, text, "hash", "plain", "Test", "test.exe", false, false, false, 0, now, now, null);
    }

    private sealed class FakeClipboardRepository : IClipboardRepository
    {
        public List<ClipboardItem> Items { get; } = [];
        public string? BlockedProcessName { get; set; }
        public Guid? IncrementedId { get; private set; }

        public Task<ClipboardItem?> FindByHashAsync(string hash, CancellationToken cancellationToken)
        {
            return Task.FromResult(Items.FirstOrDefault(item => item.Hash == hash));
        }

        public Task<ClipboardItem?> GetLatestAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(Items.OrderByDescending(item => item.CreatedAt).FirstOrDefault());
        }

        public Task IncrementUseCountAsync(Guid id, CancellationToken cancellationToken)
        {
            IncrementedId = id;
            return Task.CompletedTask;
        }

        public Task InsertAsync(ClipboardItem item, CancellationToken cancellationToken)
        {
            Items.Add(item);
            return Task.CompletedTask;
        }

        public Task<bool> IsProcessBlockedAsync(string? processName, CancellationToken cancellationToken)
        {
            return Task.FromResult(!string.IsNullOrWhiteSpace(processName) &&
                string.Equals(processName, BlockedProcessName, StringComparison.OrdinalIgnoreCase));
        }

        public Task<IReadOnlyList<ClipboardItem>> SearchAsync(string keyword, int limit, CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<ClipboardItem>>(Items);
        }
    }

    private sealed class FakeClipboardWriter : IClipboardWriter
    {
        public string? Text { get; private set; }
        public bool PasteHotkeySent { get; private set; }

        public Task SendPasteHotkeyAsync(CancellationToken cancellationToken)
        {
            PasteHotkeySent = true;
            return Task.CompletedTask;
        }

        public Task SetTextAsync(string text, CancellationToken cancellationToken)
        {
            Text = text;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeClipboardListener : IClipboardListener
    {
        public event EventHandler<ClipboardChangedEventArgs>? ClipboardChanged;
        public int StartCount { get; private set; }
        public int StopCount { get; private set; }
        public void Start() => StartCount++;
        public void Stop() => StopCount++;
        public void Raise() => ClipboardChanged?.Invoke(this, new ClipboardChangedEventArgs());
    }

    private sealed class FakeClipboardTextReader : IClipboardTextReader
    {
        public string? TryReadText() => null;
    }

    private sealed class FakeForegroundAppService : IForegroundAppService
    {
        public ForegroundAppInfo GetCurrent() => new("notepad.exe", null);
    }

    private sealed class FakeAppBlacklistService : IAppBlacklistService
    {
        private readonly FakeClipboardRepository _repository;

        public FakeAppBlacklistService(FakeClipboardRepository repository)
        {
            _repository = repository;
        }

        public Task<IReadOnlyList<Core.Privacy.AppBlacklistItem>> GetAllAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<Core.Privacy.AppBlacklistItem>>([]);
        }

        public Task AddAsync(string processName, bool blockClipboard, bool blockGesture, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task DeleteAsync(Guid id, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task UpdateAsync(Guid id, bool blockClipboard, bool blockGesture, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<bool> IsClipboardBlockedAsync(string? processName, CancellationToken cancellationToken)
        {
            return _repository.IsProcessBlockedAsync(processName, cancellationToken);
        }

        public Task<bool> IsGestureBlockedAsync(string? processName, CancellationToken cancellationToken) => Task.FromResult(false);

        public Task RefreshAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public bool IsGestureBlockedCached(string? processName) => false;
    }

    private sealed class FakeSettingsService : ISettingsService
    {
        public T Get<T>(string key, T defaultValue) => defaultValue;
        public Task SetAsync<T>(string key, T value, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
