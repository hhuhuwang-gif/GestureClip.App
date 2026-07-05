using GestureClip.Core.Abstractions;
using GestureClip.Core.Clipboard;
using GestureClip.Core.Settings;
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
    public async Task CaptureTextAsync_records_copy_count_after_successful_capture()
    {
        var repository = new FakeClipboardRepository();
        var dashboard = new FakeWorkstationDashboardService();
        var service = CreateService(repository, dashboard: dashboard);

        await service.CaptureTextAsync(Capture("normal"), CancellationToken.None);

        await WaitForAsync(() => dashboard.CopyCount == 1);
        Assert.Equal(1, dashboard.CopyCount);
    }

    [Fact]
    public async Task PasteAsync_suppresses_capture_writes_text_sends_paste_and_updates_usage()
    {
        var repository = new FakeClipboardRepository();
        var writer = new FakeClipboardWriter();
        var dashboard = new FakeWorkstationDashboardService();
        var service = CreateService(repository, writer: writer, dashboard: dashboard);
        var item = Item("hello");

        await service.PasteAsync(item, new PasteOptions(false), CancellationToken.None);

        Assert.Equal("hello", writer.Text);
        Assert.True(writer.PasteHotkeySent);
        Assert.True(service.SuppressCaptureUntil > DateTimeOffset.UtcNow);
        await WaitForAsync(() => repository.IncrementedId == item.Id && dashboard.PasteCount == 1);
        Assert.Equal(item.Id, repository.IncrementedId);
        Assert.Equal(1, dashboard.PasteCount);
    }

    [Fact]
    public async Task PasteAsync_returns_after_clipboard_write_without_waiting_for_usage_stats()
    {
        var repository = new FakeClipboardRepository();
        var releaseIncrement = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        repository.IncrementUseCountHandler = async (_, cancellationToken) =>
        {
            await releaseIncrement.Task.WaitAsync(cancellationToken);
        };
        var writer = new FakeClipboardWriter();
        var service = CreateService(repository, writer: writer);
        var item = Item("hello");

        var pasteTask = service.PasteAsync(item, new PasteOptions(false), CancellationToken.None);
        await WaitForAsync(() => writer.PasteHotkeySent);

        var completedWithoutRepository = await Task.WhenAny(pasteTask, Task.Delay(100)) == pasteTask;
        releaseIncrement.SetResult();
        await pasteTask;

        Assert.True(completedWithoutRepository);
    }

    [Fact]
    public async Task PasteAsync_image_returns_after_clipboard_write_without_waiting_for_usage_stats()
    {
        var repository = new FakeClipboardRepository();
        var releaseIncrement = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        repository.IncrementUseCountHandler = async (_, cancellationToken) =>
        {
            await releaseIncrement.Task.WaitAsync(cancellationToken);
        };
        var writer = new FakeClipboardWriter();
        var service = CreateService(repository, writer: writer);
        var now = DateTimeOffset.UtcNow;
        var image = new ClipboardItem(Guid.NewGuid(), "image/png", "png-base64", "图片", "hash", null, "Test", "test.exe", false, false, false, 0, now, now, null);

        var pasteTask = service.PasteAsync(image, new PasteOptions(false), CancellationToken.None);
        await WaitForAsync(() => writer.PasteHotkeySent);

        var completedWithoutRepository = await Task.WhenAny(pasteTask, Task.Delay(100)) == pasteTask;
        releaseIncrement.SetResult();
        await pasteTask;

        Assert.True(completedWithoutRepository);
        Assert.Equal("png-base64", writer.ImagePngBase64);
    }

    [Fact]
    public async Task CopyItemsAsync_merges_multiple_text_items_with_new_lines()
    {
        var repository = new FakeClipboardRepository();
        var writer = new FakeClipboardWriter();
        var service = CreateService(repository, writer: writer);
        var first = Item("hello");
        var second = Item("world");

        await service.CopyItemsAsync([first, second], CancellationToken.None);

        Assert.Equal("hello\r\nworld", writer.Text);
        await WaitForAsync(() => repository.IncrementedIds.Count == 2);
        Assert.Equal([first.Id, second.Id], repository.IncrementedIds);
    }

    [Fact]
    public async Task CopyItemsAsync_suppresses_capture_to_prevent_internal_copy_feedback_loop()
    {
        var repository = new FakeClipboardRepository();
        var writer = new FakeClipboardWriter();
        var service = CreateService(repository, writer: writer);

        await service.CopyItemsAsync([Item("internal copy")], CancellationToken.None);
        await service.CaptureTextAsync(Capture("internal copy"), CancellationToken.None);

        Assert.True(service.SuppressCaptureUntil > DateTimeOffset.UtcNow);
        Assert.Empty(repository.Items);
    }

    [Fact]
    public async Task CaptureTextAsync_resumes_after_suppress_window_expires()
    {
        var repository = new FakeClipboardRepository();
        var service = CreateService(repository);
        service.SuppressCaptureFor(TimeSpan.FromMilliseconds(1));
        await Task.Delay(20);

        await service.CaptureTextAsync(Capture("external copy"), CancellationToken.None);

        Assert.Single(repository.Items);
        Assert.Equal("external copy", repository.Items[0].TextContent);
    }

    [Fact]
    public async Task CopyItemsAsync_returns_after_clipboard_write_without_waiting_for_usage_updates()
    {
        var repository = new FakeClipboardRepository();
        var releaseIncrement = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        repository.IncrementUseCountHandler = async (_, cancellationToken) =>
        {
            await releaseIncrement.Task.WaitAsync(cancellationToken);
        };
        var writer = new FakeClipboardWriter();
        var service = CreateService(repository, writer: writer);
        var first = Item("hello");
        var second = Item("world");

        var copyTask = service.CopyItemsAsync([first, second], CancellationToken.None);
        await WaitForAsync(() => writer.Text == "hello\r\nworld");

        var completedWithoutUsage = await Task.WhenAny(copyTask, Task.Delay(100)) == copyTask;
        releaseIncrement.SetResult();
        await copyTask;

        Assert.True(completedWithoutUsage);
    }

    [Fact]
    public async Task CopyItemsAsync_coalesces_repeated_use_count_updates()
    {
        var repository = new FakeClipboardRepository();
        var writer = new FakeClipboardWriter();
        var service = CreateService(repository, writer: writer);
        var item = Item("hello");

        for (var index = 0; index < 20; index++)
        {
            await service.CopyItemsAsync([item], CancellationToken.None);
        }

        await WaitForAsync(() => repository.BatchedUseCounts.TryGetValue(item.Id, out var count) && count == 20);

        Assert.Equal(1, repository.BatchIncrementCallCount);
        Assert.Equal(20, repository.BatchedUseCounts[item.Id]);
    }

    [Fact]
    public async Task StopAsync_flushes_pending_use_count_updates()
    {
        var repository = new FakeClipboardRepository();
        var listener = new FakeClipboardListener();
        var writer = new FakeClipboardWriter();
        var service = CreateService(repository, listener: listener, writer: writer);
        var item = Item("hello");
        await service.StartAsync(CancellationToken.None);

        await service.CopyItemsAsync([item], CancellationToken.None);
        await service.StopAsync(CancellationToken.None);

        Assert.Equal(1, repository.BatchedUseCounts[item.Id]);
    }

    [Fact]
    public async Task CopyItemsAsync_restores_single_image_to_clipboard()
    {
        var repository = new FakeClipboardRepository();
        var writer = new FakeClipboardWriter();
        var service = CreateService(repository, writer: writer);
        var now = DateTimeOffset.UtcNow;
        var image = new ClipboardItem(Guid.NewGuid(), "image/png", "png-base64", "图片", "hash", null, "Test", "test.exe", false, false, false, 0, now, now, null);

        await service.CopyItemsAsync([image], CancellationToken.None);

        Assert.Equal("png-base64", writer.ImagePngBase64);
        await WaitForAsync(() => repository.IncrementedIds.Count == 1);
        Assert.Equal([image.Id], repository.IncrementedIds);
    }

    [Fact]
    public async Task CopyItemsAsync_loads_full_image_content_when_list_item_only_has_thumbnail()
    {
        var repository = new FakeClipboardRepository();
        var writer = new FakeClipboardWriter();
        var service = CreateService(repository, writer: writer);
        var now = DateTimeOffset.UtcNow;
        var full = new ClipboardItem(Guid.NewGuid(), "image/png", "full-image-base64", "图片", "hash", null, "Test", "test.exe", false, false, false, 0, now, now, null, "thumb-base64");
        var listItem = full with { TextContent = null };
        repository.Items.Add(full);

        await service.CopyItemsAsync([listItem], CancellationToken.None);

        Assert.Equal("full-image-base64", writer.ImagePngBase64);
    }

    [Fact]
    public async Task CopyItemsAsync_rejects_mixed_text_and_image_items()
    {
        var repository = new FakeClipboardRepository();
        var service = CreateService(repository);
        var now = DateTimeOffset.UtcNow;
        var image = new ClipboardItem(Guid.NewGuid(), "image/png", "png-base64", "图片", "hash", null, "Test", "test.exe", false, false, false, 0, now, now, null);

        await Assert.ThrowsAsync<NotSupportedException>(() => service.CopyItemsAsync([Item("hello"), image], CancellationToken.None));
    }

    [Fact]
    public async Task Clipboard_changed_captures_image_when_text_is_not_available()
    {
        var repository = new FakeClipboardRepository();
        var listener = new FakeClipboardListener();
        var reader = new FakeClipboardTextReader { ImagePngBase64 = "png-base64" };
        var service = CreateService(repository, listener: listener, reader: reader);

        await service.StartAsync(CancellationToken.None);
        listener.Raise();
        await WaitForAsync(() => repository.Items.Count == 1);

        Assert.Equal("image/png", repository.Items[0].ContentType);
        Assert.Equal("png-base64", repository.Items[0].TextContent);
    }

    [Fact]
    public async Task Clipboard_changed_prefers_image_when_clipboard_contains_text_and_image()
    {
        var repository = new FakeClipboardRepository();
        var listener = new FakeClipboardListener();
        var reader = new FakeClipboardTextReader
        {
            Text = "image alt text",
            ImagePngBase64 = "png-base64"
        };
        var service = CreateService(repository, listener: listener, reader: reader);

        await service.StartAsync(CancellationToken.None);
        listener.Raise();
        await WaitForAsync(() => repository.Items.Count == 1);

        Assert.Equal("image/png", repository.Items[0].ContentType);
        Assert.Equal("png-base64", repository.Items[0].TextContent);
    }

    [Fact]
    public async Task Clipboard_changed_skips_image_when_base64_exceeds_configured_limit()
    {
        var repository = new FakeClipboardRepository();
        var listener = new FakeClipboardListener();
        var settings = new FakeSettingsService();
        settings.Values[SettingKeys.ClipboardMaxImageBytes] = 128 * 1024;
        var reader = new FakeClipboardTextReader { ImagePngBase64 = Convert.ToBase64String(new byte[(128 * 1024) + 1]) };
        var service = CreateService(repository, listener: listener, reader: reader, settings: settings);

        await service.StartAsync(CancellationToken.None);
        listener.Raise();
        await Task.Delay(80);

        Assert.Empty(repository.Items);
    }

    [Fact]
    public async Task Clipboard_changed_events_are_processed_serially()
    {
        var repository = new FakeClipboardRepository();
        var firstInsertStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirstInsert = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondInsertStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var insertCount = 0;
        repository.InsertHandler = async (_, cancellationToken) =>
        {
            if (Interlocked.Increment(ref insertCount) == 1)
            {
                firstInsertStarted.SetResult();
                await releaseFirstInsert.Task.WaitAsync(cancellationToken);
                return;
            }

            secondInsertStarted.SetResult();
        };

        var listener = new FakeClipboardListener();
        var reader = new FakeClipboardTextReader();
        reader.TextQueue.Enqueue("first");
        reader.TextQueue.Enqueue("second");
        var service = CreateService(repository, listener: listener, reader: reader);

        await service.StartAsync(CancellationToken.None);
        listener.Raise();
        listener.Raise();
        await firstInsertStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));

        var secondStartedBeforeFirstReleased = await Task.WhenAny(secondInsertStarted.Task, Task.Delay(100)) == secondInsertStarted.Task;
        releaseFirstInsert.SetResult();
        await WaitForAsync(() => repository.Items.Count == 2);

        Assert.False(secondStartedBeforeFirstReleased);
    }

    [Fact]
    public async Task Clipboard_perf_setting_is_not_reloaded_for_every_perf_event()
    {
        var repository = new FakeClipboardRepository();
        var listener = new FakeClipboardListener();
        var reader = new FakeClipboardTextReader { Text = "normal" };
        var settings = new FakeSettingsService();
        var service = CreateService(repository, listener: listener, reader: reader, settings: settings);

        await service.StartAsync(CancellationToken.None);
        listener.Raise();
        await WaitForAsync(() => repository.Items.Count == 1);

        Assert.True(settings.GetCount(SettingKeys.ClipboardPerfLogEnabled) <= 1);
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

    [Fact]
    public void ClipboardService_exposes_stage10_performance_metric_names()
    {
        var source = string.Join(
            Environment.NewLine,
            File.ReadAllText(FindRepositoryFile("src", "GestureClip.Features", "Clipboard", "ClipboardService.cs")),
            File.ReadAllText(FindRepositoryFile("src", "GestureClip.App", "ViewModels", "ClipboardOverlayViewModel.cs")),
            File.ReadAllText(FindRepositoryFile("src", "GestureClip.App", "Services", "GestureOverlayService.cs")));

        Assert.Contains("ClipboardCopyDurationMs", source);
        Assert.Contains("DbUpdateDurationMs", source);
        Assert.Contains("UiRefreshDurationMs", source);
        Assert.Contains("SearchDurationMs", source);
        Assert.Contains("ThumbnailDecodeDurationMs", source);
        Assert.Contains("HudUpdateDurationMs", source);
    }

    private static ClipboardService CreateService(
        FakeClipboardRepository repository,
        FakeClipboardWriter? writer = null,
        FakeClipboardListener? listener = null,
        FakeClipboardTextReader? reader = null,
        FakeSettingsService? settings = null,
        FakeWorkstationDashboardService? dashboard = null)
    {
        return new ClipboardService(
            listener ?? new FakeClipboardListener(),
            reader ?? new FakeClipboardTextReader(),
            writer ?? new FakeClipboardWriter(),
            repository,
            new ClipboardHashService(),
            new SensitiveContentDetector(),
            new FakeForegroundAppService(),
            new FakeAppBlacklistService(repository),
            settings ?? new FakeSettingsService(),
            dashboard ?? new FakeWorkstationDashboardService(),
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

    private static string FindRepositoryFile(params string[] segments)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(new[] { directory.FullName }.Concat(segments).ToArray());
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("Could not locate repository file.", Path.Combine(segments));
    }

    private sealed class FakeClipboardRepository : IClipboardRepository
    {
        public List<ClipboardItem> Items { get; } = [];
        public string? BlockedProcessName { get; set; }
        public Guid? IncrementedId { get; private set; }
        public List<Guid> IncrementedIds { get; } = [];
        public Dictionary<Guid, int> BatchedUseCounts { get; } = [];
        public int BatchIncrementCallCount { get; private set; }
        public Func<ClipboardItem, CancellationToken, Task>? InsertHandler { get; set; }
        public Func<Guid, CancellationToken, Task>? IncrementUseCountHandler { get; set; }

        public Task<ClipboardItem?> FindByHashAsync(string hash, CancellationToken cancellationToken)
        {
            return Task.FromResult(Items.FirstOrDefault(item => item.Hash == hash));
        }

        public Task<ClipboardItem?> GetLatestAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(Items.OrderByDescending(item => item.CreatedAt).FirstOrDefault());
        }

        public Task<ClipboardItem?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
        {
            return Task.FromResult(Items.FirstOrDefault(item => item.Id == id));
        }

        public async Task IncrementUseCountAsync(Guid id, CancellationToken cancellationToken)
        {
            if (IncrementUseCountHandler is not null)
            {
                await IncrementUseCountHandler(id, cancellationToken);
            }

            IncrementedId = id;
            IncrementedIds.Add(id);
        }

        public async Task IncrementUseCountsAsync(IReadOnlyDictionary<Guid, int> increments, CancellationToken cancellationToken)
        {
            BatchIncrementCallCount++;
            foreach (var (id, count) in increments)
            {
                for (var index = 0; index < count; index++)
                {
                    await IncrementUseCountAsync(id, cancellationToken);
                }

                BatchedUseCounts[id] = BatchedUseCounts.TryGetValue(id, out var existing)
                    ? existing + count
                    : count;
            }
        }

        public Task<int> DeleteAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken)
        {
            var deleted = Items.RemoveAll(item => ids.Contains(item.Id));
            return Task.FromResult(deleted);
        }

        public Task SetPinnedAsync(Guid id, bool isPinned, CancellationToken cancellationToken)
        {
            var index = Items.FindIndex(item => item.Id == id);
            if (index >= 0)
            {
                Items[index] = Items[index] with { IsPinned = isPinned };
            }

            return Task.CompletedTask;
        }

        public Task SetFavoriteAsync(Guid id, bool isFavorite, CancellationToken cancellationToken)
        {
            var index = Items.FindIndex(item => item.Id == id);
            if (index >= 0)
            {
                Items[index] = Items[index] with { IsFavorite = isFavorite };
            }

            return Task.CompletedTask;
        }

        public async Task InsertAsync(ClipboardItem item, CancellationToken cancellationToken)
        {
            if (InsertHandler is not null)
            {
                await InsertHandler(item, cancellationToken);
            }

            Items.Add(item);
        }

        public Task<bool> IsProcessBlockedAsync(string? processName, CancellationToken cancellationToken)
        {
            return Task.FromResult(!string.IsNullOrWhiteSpace(processName) &&
                string.Equals(processName, BlockedProcessName, StringComparison.OrdinalIgnoreCase));
        }

        public Task<int> GetCountAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(Items.Count);
        }

        public Task<int> ClearAllAsync(CancellationToken cancellationToken)
        {
            var count = Items.Count;
            Items.Clear();
            return Task.FromResult(count);
        }

        public Task<int> ClearUnpinnedAsync(CancellationToken cancellationToken)
        {
            var deleted = Items.RemoveAll(item => !item.IsPinned);
            return Task.FromResult(deleted);
        }

        public Task<int> CleanupAsync(int maxItems, int retentionDays, CancellationToken cancellationToken)
        {
            return Task.FromResult(0);
        }

        public Task<IReadOnlyList<ClipboardItem>> SearchAsync(string keyword, int limit, CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<ClipboardItem>>(Items);
        }
    }

    private sealed class FakeClipboardWriter : IClipboardWriter
    {
        public string? Text { get; private set; }
        public string? ImagePngBase64 { get; private set; }
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

        public Task SetImagePngBase64Async(string pngBase64, CancellationToken cancellationToken)
        {
            ImagePngBase64 = pngBase64;
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
        public string? Text { get; set; }
        public string? ImagePngBase64 { get; set; }
        public Queue<string?> TextQueue { get; } = [];

        public string? TryReadText() => TextQueue.TryDequeue(out var text) ? text : Text;

        public string? TryReadImagePngBase64() => ImagePngBase64;
    }

    private static async Task WaitForAsync(Func<bool> condition)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(2);
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(20);
        }

        Assert.True(condition());
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
        public Dictionary<string, object?> Values { get; } = [];
        private readonly Dictionary<string, int> _getCounts = [];

        public T Get<T>(string key, T defaultValue)
        {
            _getCounts[key] = _getCounts.TryGetValue(key, out var count) ? count + 1 : 1;
            return Values.TryGetValue(key, out var value) ? (T)value! : defaultValue;
        }

        public Task SetAsync<T>(string key, T value, CancellationToken cancellationToken) => Task.CompletedTask;

        public int GetCount(string key) => _getCounts.TryGetValue(key, out var count) ? count : 0;
    }

    private sealed class FakeWorkstationDashboardService : IWorkstationDashboardService
    {
        public int CopyCount { get; private set; }

        public int PasteCount { get; private set; }

        public Task<Core.Workstation.WorkstationDashboardSnapshot> GetSnapshotAsync(DateTimeOffset now, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task StartFishingAsync(DateTimeOffset now, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task EndFishingAsync(DateTimeOffset now, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task ResetTodayAsync(DateOnly date, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task RecordCopyAsync(DateTimeOffset now, CancellationToken cancellationToken)
        {
            CopyCount++;
            return Task.CompletedTask;
        }

        public Task RecordPasteAsync(DateTimeOffset now, CancellationToken cancellationToken)
        {
            PasteCount++;
            return Task.CompletedTask;
        }

        public Task RecordGestureAsync(DateTimeOffset now, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
