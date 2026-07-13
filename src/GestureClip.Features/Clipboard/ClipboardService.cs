using System.Diagnostics;
using System.Threading.Channels;
using GestureClip.Core.Abstractions;
using GestureClip.Core.Clipboard;
using GestureClip.Core.Settings;
using GestureClip.Features.Assistant;
using GestureClip.Features.Gestures;
using Microsoft.Extensions.Logging;

namespace GestureClip.Features.Clipboard;

public sealed class ClipboardService : IClipboardService
{
    private const int DefaultMaxImageBytes = 5 * 1024 * 1024;
    private const int MinMaxImageBytes = 128 * 1024;
    private const int MaxMaxImageBytes = 20 * 1024 * 1024;

    private readonly IClipboardListener _clipboardListener;
    private readonly IClipboardTextReader _clipboardTextReader;
    private readonly IClipboardWriter _clipboardWriter;
    private readonly IClipboardRepository _clipboardRepository;
    private readonly IClipboardHashService _clipboardHashService;
    private readonly ISensitiveContentDetector _sensitiveContentDetector;
    private readonly ISensitiveCaptureGate? _sensitiveCaptureGate;
    private readonly IForegroundAppService _foregroundAppService;
    private readonly IAppBlacklistService _appBlacklistService;
    private readonly ISettingsService _settingsService;
    private readonly IWorkstationDashboardService _workstationDashboardService;
    private readonly ILogger<ClipboardService> _logger;
    private int _started;
    private Channel<ClipboardChangedEventArgs>? _captureQueue;
    private CancellationTokenSource? _queueCancellation;
    private Task? _queueTask;
    private readonly bool _perfLogEnabled;
    private readonly object _usageSync = new();
    private readonly Dictionary<Guid, int> _pendingUseCounts = [];
    private Task? _usageFlushTask;

    public ClipboardService(
        IClipboardListener clipboardListener,
        IClipboardTextReader clipboardTextReader,
        IClipboardWriter clipboardWriter,
        IClipboardRepository clipboardRepository,
        IClipboardHashService clipboardHashService,
        ISensitiveContentDetector sensitiveContentDetector,
        IForegroundAppService foregroundAppService,
        IAppBlacklistService appBlacklistService,
        ISettingsService settingsService,
        IWorkstationDashboardService workstationDashboardService,
        ILogger<ClipboardService> logger,
        ISensitiveCaptureGate? sensitiveCaptureGate = null)
    {
        _clipboardListener = clipboardListener;
        _clipboardTextReader = clipboardTextReader;
        _clipboardWriter = clipboardWriter;
        _clipboardRepository = clipboardRepository;
        _clipboardHashService = clipboardHashService;
        _sensitiveContentDetector = sensitiveContentDetector;
        _sensitiveCaptureGate = sensitiveCaptureGate;
        _foregroundAppService = foregroundAppService;
        _appBlacklistService = appBlacklistService;
        _settingsService = settingsService;
        _workstationDashboardService = workstationDashboardService;
        _logger = logger;
        IsCaptureEnabled = _settingsService.Get(SettingKeys.ClipboardCaptureEnabled, true);
        _perfLogEnabled = _settingsService.Get(SettingKeys.ClipboardPerfLogEnabled, false) ||
            _settingsService.Get(SettingKeys.GestureDebugEnabled, false);
    }

    public bool IsCaptureEnabled { get; private set; }

    public DateTimeOffset? SuppressCaptureUntil { get; private set; }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (Interlocked.Exchange(ref _started, 1) == 1)
        {
            return Task.CompletedTask;
        }

        _queueCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _captureQueue = Channel.CreateBounded<ClipboardChangedEventArgs>(new BoundedChannelOptions(128)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.DropOldest
        });
        _queueTask = Task.Run(() => ProcessClipboardQueueAsync(_queueCancellation.Token), CancellationToken.None);
        _clipboardListener.ClipboardChanged += OnClipboardChanged;
        try
        {
            _clipboardListener.Start();
            _logger.LogInformation("Clipboard text history service started.");
        }
        catch
        {
            _clipboardListener.ClipboardChanged -= OnClipboardChanged;
            _captureQueue.Writer.TryComplete();
            _queueCancellation.Cancel();
            Interlocked.Exchange(ref _started, 0);
            throw;
        }

        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (Interlocked.Exchange(ref _started, 0) == 0)
        {
            return;
        }

        _clipboardListener.ClipboardChanged -= OnClipboardChanged;
        _clipboardListener.Stop();
        _captureQueue?.Writer.TryComplete();
        _queueCancellation?.Cancel();
        if (_queueTask is not null)
        {
            try
            {
                await _queueTask.WaitAsync(TimeSpan.FromSeconds(1), cancellationToken);
            }
            catch (OperationCanceledException)
            {
            }
            catch (TimeoutException)
            {
                _logger.LogWarning("Clipboard capture queue did not stop within timeout.");
            }
        }

        _queueCancellation?.Dispose();
        _queueCancellation = null;
        _captureQueue = null;
        _queueTask = null;
        await FlushPendingUseCountsAsync(cancellationToken);
        _logger.LogInformation("Clipboard text history service stopped.");
    }

    public async Task SetCaptureEnabledAsync(bool enabled, CancellationToken cancellationToken)
    {
        IsCaptureEnabled = enabled;
        await _settingsService.SetAsync(SettingKeys.ClipboardCaptureEnabled, enabled, cancellationToken);
        _logger.LogInformation("Clipboard capture state changed to {CaptureEnabled}.", enabled);
    }

    public void SuppressCaptureFor(TimeSpan duration)
    {
        SuppressCaptureUntil = DateTimeOffset.UtcNow.Add(duration);
    }

    public async Task CaptureTextAsync(ClipboardCapture capture, CancellationToken cancellationToken)
    {
        if (!IsCaptureEnabled)
        {
            _logger.LogInformation("Clipboard capture skipped: disabled.");
            return;
        }

        if (SuppressCaptureUntil is { } suppressUntil && DateTimeOffset.UtcNow < suppressUntil)
        {
            _logger.LogInformation("Clipboard capture skipped: suppressed after internal paste.");
            return;
        }

        if (await _appBlacklistService.IsClipboardBlockedAsync(capture.SourceProcess, cancellationToken))
        {
            _logger.LogInformation("Clipboard capture skipped: source process is blacklisted.");
            return;
        }

        if (_sensitiveCaptureGate?.ShouldSkipCapture(capture.SourceProcess, capture.SourceApp) == true)
        {
            _logger.LogInformation("Clipboard capture skipped: sensitive UI context (password/login).");
            return;
        }

        if (string.IsNullOrWhiteSpace(capture.Text))
        {
            _logger.LogInformation("Clipboard capture skipped: empty text.");
            return;
        }

        var hashWatch = Stopwatch.StartNew();
        var hash = _clipboardHashService.ComputeHash(capture.Text);
        var plainTextHash = _clipboardHashService.ComputePlainTextHash(capture.Text);
        hashWatch.Stop();
        LogPerf("HashMs", hashWatch.ElapsedMilliseconds, ("ContentType", "text"));

        var dedupWatch = Stopwatch.StartNew();
        var existing = await _clipboardRepository.FindByHashAsync(hash, cancellationToken);
        dedupWatch.Stop();
        LogPerf("DedupQueryMs", dedupWatch.ElapsedMilliseconds, ("ContentType", "text"));
        if (existing is not null)
        {
            await _clipboardRepository.TouchAsync(existing.Id, cancellationToken);
            RecordCopyInBackground(DateTimeOffset.UtcNow);
            _logger.LogInformation("Clipboard duplicate item refreshed.");
            return;
        }

        var isSensitive = _sensitiveContentDetector.LooksSensitive(capture.Text);
        if (isSensitive && _settingsService.Get(SettingKeys.PrivacySuppressSensitive, true))
        {
            _logger.LogInformation("Clipboard capture skipped: sensitive text detected.");
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var item = new ClipboardItem(
            Guid.NewGuid(),
            "text",
            capture.Text,
            CreatePreview(capture.Text),
            hash,
            plainTextHash,
            capture.SourceApp,
            capture.SourceProcess,
            false,
            false,
            isSensitive,
            0,
            now,
            now,
            null);

        var insertWatch = Stopwatch.StartNew();
        await _clipboardRepository.InsertAsync(item, cancellationToken);
        insertWatch.Stop();
        LogPerf("InsertMs", insertWatch.ElapsedMilliseconds, ("ContentType", "text"));
        RecordCopyInBackground(now);
        _logger.LogInformation("Clipboard text item captured.");
    }

    public async Task<IReadOnlyList<ClipboardItem>> SearchAsync(string keyword, int limit, CancellationToken cancellationToken)
    {
        return await SearchAsync(keyword, limit, 0, cancellationToken);
    }

    public async Task<IReadOnlyList<ClipboardItem>> SearchAsync(string keyword, int limit, int offset, CancellationToken cancellationToken)
    {
        var watch = Stopwatch.StartNew();
        var results = await _clipboardRepository.SearchAsync(keyword, limit, offset, cancellationToken);
        watch.Stop();
        LogPerf("SearchMs", watch.ElapsedMilliseconds, ("ResultCount", results.Count));
        LogPerf("SearchDurationMs", watch.ElapsedMilliseconds, ("ResultCount", results.Count));
        return results;
    }

    public async Task<IReadOnlyList<ClipboardItem>> SearchAsync(
        string keyword,
        int limit,
        int offset,
        ClipboardContentFilter filter,
        CancellationToken cancellationToken)
    {
        var watch = Stopwatch.StartNew();
        var results = await _clipboardRepository.SearchAsync(keyword, limit, offset, filter, cancellationToken);
        watch.Stop();
        LogPerf("SearchMs", watch.ElapsedMilliseconds, ("ResultCount", results.Count), ("Filter", filter.ToString()));
        LogPerf("SearchDurationMs", watch.ElapsedMilliseconds, ("ResultCount", results.Count), ("Filter", filter.ToString()));
        return results;
    }

    public Task<ClipboardItem?> GetLatestAsync(CancellationToken cancellationToken)
    {
        return _clipboardRepository.GetLatestAsync(cancellationToken);
    }

    public Task<ClipboardItem?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return _clipboardRepository.GetByIdAsync(id, cancellationToken);
    }

    public async Task PasteAsync(ClipboardItem item, PasteOptions options, CancellationToken cancellationToken)
    {
        var pasteWatch = Stopwatch.StartNew();
        if (item.IsImage)
        {
            var image = await EnsureFullContentAsync(item, cancellationToken);
            if (string.IsNullOrWhiteSpace(image.TextContent))
            {
                return;
            }

            SuppressCaptureFor(TimeSpan.FromMilliseconds(1500));
            await _clipboardWriter.SetImagePngBase64Async(image.TextContent, cancellationToken);
            await Task.Delay(70, cancellationToken);
            await _clipboardWriter.SendPasteHotkeyAsync(cancellationToken);
            await RecordPasteUsageAsync(image.Id, DateTimeOffset.UtcNow, cancellationToken);
            pasteWatch.Stop();
            LogPerf("PasteMs", pasteWatch.ElapsedMilliseconds, ("ContentType", "image/png"));
            return;
        }

        var textItem = await EnsureFullContentAsync(item, cancellationToken);
        if (string.IsNullOrEmpty(textItem.TextContent))
        {
            return;
        }

        var pasteText = textItem.TextContent;
        if (options.PlainText)
        {
            pasteText = LocalTextTransforms.ToPlainText(pasteText);
        }
        else if (_settingsService.Get(SettingKeys.SmartPasteEnabled, true))
        {
            var strategy = SmartPastePolicy.Select(_foregroundAppService.GetCurrent());
            pasteText = SmartPastePolicy.TransformForStrategy(pasteText, strategy);
        }

        SuppressCaptureFor(TimeSpan.FromMilliseconds(1500));
        await _clipboardWriter.SetTextAsync(pasteText, cancellationToken);
        await Task.Delay(70, cancellationToken);
        await _clipboardWriter.SendPasteHotkeyAsync(cancellationToken);
        await RecordPasteUsageAsync(textItem.Id, DateTimeOffset.UtcNow, cancellationToken);
        pasteWatch.Stop();
        LogPerf("PasteMs", pasteWatch.ElapsedMilliseconds, ("ContentType", "text"));
        _logger.LogInformation("Clipboard text item pasted.");
    }

    public async Task CopyItemsAsync(IReadOnlyList<ClipboardItem> items, CancellationToken cancellationToken)
    {
        var copyWatch = Stopwatch.StartNew();
        if (items.Count == 0)
        {
            return;
        }

        var hasText = items.Any(item => item.IsText);
        var hasImage = items.Any(item => item.IsImage);
        if (hasText && hasImage)
        {
            throw new NotSupportedException("混合内容暂不支持批量复制。");
        }

        SuppressCaptureFor(TimeSpan.FromMilliseconds(1000));
        if (hasImage)
        {
            if (items.Count > 1)
            {
                throw new NotSupportedException("暂不支持批量复制多张图片。");
            }

            var image = await EnsureFullContentAsync(items[0], cancellationToken);
            if (string.IsNullOrWhiteSpace(image.TextContent))
            {
                return;
            }

            await _clipboardWriter.SetImagePngBase64Async(image.TextContent, cancellationToken);
            RecordUseCountInBackground([image.Id]);
            copyWatch.Stop();
            LogPerf("ClipboardCopyDurationMs", copyWatch.ElapsedMilliseconds, ("ContentType", "image/png"), ("ItemCount", items.Count));
            _logger.LogInformation("Clipboard image item copied.");
            return;
        }

        var textItems = new List<ClipboardItem>(items.Count);
        foreach (var item in items)
        {
            textItems.Add(await EnsureFullContentAsync(item, cancellationToken));
        }

        var text = string.Join("\r\n", textItems.Select(item => item.TextContent).Where(text => !string.IsNullOrEmpty(text)));
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        await _clipboardWriter.SetTextAsync(text, cancellationToken);
        await WaitForClipboardSettleAsync(cancellationToken);
        RecordUseCountInBackground(textItems.Select(item => item.Id).ToArray());
        copyWatch.Stop();
        LogPerf("ClipboardCopyDurationMs", copyWatch.ElapsedMilliseconds, ("ContentType", "text"), ("ItemCount", items.Count));

        _logger.LogInformation("Clipboard text items copied. Count={ClipboardItemCount}", items.Count);
    }

    public async Task<int> DeleteItemsAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken)
    {
        var deleted = await _clipboardRepository.DeleteAsync(ids, cancellationToken);
        _logger.LogInformation("Clipboard items deleted. Count={ClipboardItemCount}", deleted);
        return deleted;
    }

    public async Task RestoreItemsAsync(IReadOnlyList<ClipboardItem> items, CancellationToken cancellationToken)
    {
        if (items.Count == 0)
        {
            return;
        }

        foreach (var item in items)
        {
            var existing = await _clipboardRepository.GetByIdAsync(item.Id, cancellationToken);
            if (existing is not null)
            {
                continue;
            }

            await _clipboardRepository.InsertAsync(item, cancellationToken);
        }

        _logger.LogInformation("Clipboard items restored. Count={ClipboardItemCount}", items.Count);
    }

    public async Task SetPinnedAsync(Guid id, bool isPinned, CancellationToken cancellationToken)
    {
        await _clipboardRepository.SetPinnedAsync(id, isPinned, cancellationToken);
        _logger.LogInformation("Clipboard item pin state changed. IsPinned={IsPinned}", isPinned);
    }

    public async Task SetFavoriteAsync(Guid id, bool isFavorite, CancellationToken cancellationToken)
    {
        await _clipboardRepository.SetFavoriteAsync(id, isFavorite, cancellationToken);
        _logger.LogInformation("Clipboard item favorite state changed. IsFavorite={IsFavorite}", isFavorite);
    }

    private void OnClipboardChanged(object? sender, ClipboardChangedEventArgs e)
    {
        LogPerf("ListenerReceived", 0);
        if (_captureQueue is { } queue && queue.Writer.TryWrite(e))
        {
            return;
        }

        _logger.LogDebug("Clipboard capture queue unavailable or full; clipboard event dropped.");
    }

    private async Task ProcessClipboardQueueAsync(CancellationToken cancellationToken)
    {
        var reader = _captureQueue?.Reader;
        if (reader is null)
        {
            return;
        }

        try
        {
            await foreach (var item in reader.ReadAllAsync(cancellationToken))
            {
                await HandleClipboardChangedAsync(item, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Clipboard capture queue stopped unexpectedly.");
        }
    }

    private async Task HandleClipboardChangedAsync(ClipboardChangedEventArgs e)
    {
        await HandleClipboardChangedAsync(e, CancellationToken.None);
    }

    private async Task HandleClipboardChangedAsync(ClipboardChangedEventArgs e, CancellationToken cancellationToken)
    {
        try
        {
            var textWatch = Stopwatch.StartNew();
            var text = _clipboardTextReader.TryReadText();
            textWatch.Stop();
            LogPerf("TextReadMs", textWatch.ElapsedMilliseconds, ("HasText", text is not null));
            if (!string.IsNullOrWhiteSpace(text))
            {
                var foreground = _foregroundAppService.GetCurrent();
                await CaptureTextAsync(
                    new ClipboardCapture(text, foreground.WindowTitle, foreground.ProcessName, e.ChangedAt),
                    cancellationToken);
                return;
            }

            var imageWatch = Stopwatch.StartNew();
            var imagePngBase64 = _clipboardTextReader.TryReadImagePngBase64();
            imageWatch.Stop();
            LogPerf("ImageReadMs", imageWatch.ElapsedMilliseconds, ("HasImage", imagePngBase64 is not null));
            if (imagePngBase64 is not null)
            {
                await CaptureImageAsync(imagePngBase64, e.ChangedAt, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Clipboard changed handling failed.");
        }
    }

    private async Task CaptureImageAsync(string pngBase64, DateTimeOffset changedAt, CancellationToken cancellationToken)
    {
        if (!IsCaptureEnabled)
        {
            _logger.LogInformation("Clipboard image capture skipped: disabled.");
            return;
        }

        if (SuppressCaptureUntil is { } suppressUntil && DateTimeOffset.UtcNow < suppressUntil)
        {
            _logger.LogInformation("Clipboard image capture skipped: suppressed after internal paste.");
            return;
        }

        var imageBytes = EstimateBase64DecodedByteCount(pngBase64);
        var maxImageBytes = GetMaxImageBytes();
        if (imageBytes > maxImageBytes)
        {
            _logger.LogInformation(
                "Clipboard image capture skipped: image is too large. ImageBytes={ImageBytes}, MaxImageBytes={MaxImageBytes}",
                imageBytes,
                maxImageBytes);
            return;
        }

        var foreground = _foregroundAppService.GetCurrent();
        if (await _appBlacklistService.IsClipboardBlockedAsync(foreground.ProcessName, cancellationToken))
        {
            _logger.LogInformation("Clipboard image capture skipped: source process is blacklisted.");
            return;
        }

        var hashWatch = Stopwatch.StartNew();
        var hash = _clipboardHashService.ComputeHash(pngBase64);
        hashWatch.Stop();
        LogPerf("HashMs", hashWatch.ElapsedMilliseconds, ("ContentType", "image/png"));

        var dedupWatch = Stopwatch.StartNew();
        var duplicate = await _clipboardRepository.FindByHashAsync(hash, cancellationToken);
        dedupWatch.Stop();
        LogPerf("DedupQueryMs", dedupWatch.ElapsedMilliseconds, ("ContentType", "image/png"));
        if (duplicate is not null)
        {
            _logger.LogInformation("Clipboard image capture skipped: duplicate hash.");
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var insertWatch = Stopwatch.StartNew();
        var thumbnailWatch = Stopwatch.StartNew();
        var thumbnailContent = await Task.Run(
            () => GestureClip.Infrastructure.Clipboard.ClipboardImageFactory.TryCreateThumbnailPngBase64(pngBase64, 128),
            cancellationToken);
        thumbnailWatch.Stop();
        LogPerf("ThumbnailDecodeDurationMs", thumbnailWatch.ElapsedMilliseconds, ("ContentType", "image/png"));

        await _clipboardRepository.InsertAsync(
            new ClipboardItem(
                Guid.NewGuid(),
                "image/png",
                pngBase64,
                "图片",
                hash,
                null,
                foreground.WindowTitle,
                foreground.ProcessName,
                false,
                false,
                false,
                0,
                now,
                now,
                null,
                thumbnailContent),
            cancellationToken);
        insertWatch.Stop();
        LogPerf("InsertMs", insertWatch.ElapsedMilliseconds, ("ContentType", "image/png"));
        RecordCopyInBackground(now);
        _logger.LogInformation("Clipboard image item captured.");
    }

    private void RecordCopyInBackground(DateTimeOffset now)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await _workstationDashboardService.RecordCopyAsync(now, CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Clipboard copy stats recording failed.");
            }
        });
    }

    private async Task<ClipboardItem> EnsureFullContentAsync(ClipboardItem item, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(item.TextContent))
        {
            return item;
        }

        var fullItem = await _clipboardRepository.GetByIdAsync(item.Id, cancellationToken);
        return fullItem ?? item;
    }

    private async Task RecordPasteUsageAsync(Guid itemId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        RecordUseCountInBackground([itemId]);
        await RecordPasteStatsAsync(now, cancellationToken);
    }

    private static Task WaitForClipboardSettleAsync(CancellationToken cancellationToken)
    {
        return Task.Delay(TimeSpan.FromMilliseconds(60), cancellationToken);
    }

    private void RecordUseCountInBackground(IReadOnlyList<Guid> itemIds)
    {
        if (itemIds.Count == 0)
        {
            return;
        }

        lock (_usageSync)
        {
            foreach (var itemId in itemIds)
            {
                _pendingUseCounts[itemId] = _pendingUseCounts.TryGetValue(itemId, out var count)
                    ? count + 1
                    : 1;
            }

            if (_usageFlushTask is not null && !_usageFlushTask.IsCompleted)
            {
                return;
            }

            _usageFlushTask = Task.Run(DelayedFlushUseCountsAsync);
        }
    }

    private async Task DelayedFlushUseCountsAsync()
    {
        try
        {
            await Task.Delay(250);
            await FlushPendingUseCountsAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Clipboard use count batch recording failed.");
        }
    }

    private async Task FlushPendingUseCountsAsync(CancellationToken cancellationToken)
    {
        Dictionary<Guid, int> snapshot;
        lock (_usageSync)
        {
            if (_pendingUseCounts.Count == 0)
            {
                return;
            }

            snapshot = new Dictionary<Guid, int>(_pendingUseCounts);
            _pendingUseCounts.Clear();
        }

        try
        {
            var watch = Stopwatch.StartNew();
            await _clipboardRepository.IncrementUseCountsAsync(snapshot, cancellationToken);
            watch.Stop();
            LogPerf("DbUpdateDurationMs", watch.ElapsedMilliseconds, ("Operation", "IncrementUseCounts"), ("ItemCount", snapshot.Count));
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Clipboard use count batch recording failed.");
        }
    }

    private async Task RecordPasteStatsAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        try
        {
            await _workstationDashboardService.RecordPasteAsync(now, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Clipboard paste stats recording failed.");
        }
    }

    private bool IsPerfLogEnabled()
    {
        return _perfLogEnabled;
    }

    private void LogPerf(string eventName, long elapsedMs, params (string Key, object? Value)[] values)
    {
        if (!IsPerfLogEnabled())
        {
            return;
        }

        var details = values.Length == 0
            ? string.Empty
            : " " + string.Join(" ", values.Select(value => $"{value.Key}={value.Value}"));
        _logger.LogInformation("ClipboardPerf {EventName} ElapsedMs={ElapsedMs}{Details}", eventName, elapsedMs, details);
    }

    private int GetMaxImageBytes()
    {
        var configured = _settingsService.Get(SettingKeys.ClipboardMaxImageBytes, DefaultMaxImageBytes);
        if (configured <= 0)
        {
            return DefaultMaxImageBytes;
        }

        return Math.Clamp(configured, MinMaxImageBytes, MaxMaxImageBytes);
    }

    private static long EstimateBase64DecodedByteCount(string value)
    {
        var base64 = NormalizeBase64(value);
        if (base64.Length == 0)
        {
            return 0;
        }

        var padding = 0;
        if (base64.EndsWith("==", StringComparison.Ordinal))
        {
            padding = 2;
        }
        else if (base64.EndsWith("=", StringComparison.Ordinal))
        {
            padding = 1;
        }

        return (base64.Length * 3L / 4L) - padding;
    }

    private static string NormalizeBase64(string value)
    {
        var trimmed = value.Trim();
        var commaIndex = trimmed.IndexOf(',', StringComparison.Ordinal);
        if (trimmed.StartsWith("data:", StringComparison.OrdinalIgnoreCase) && commaIndex >= 0)
        {
            trimmed = trimmed[(commaIndex + 1)..];
        }

        return new string(trimmed.Where(character => !char.IsWhiteSpace(character)).ToArray());
    }

    private static string CreatePreview(string text)
    {
        var compact = text.Replace("\r\n", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal)
            .Replace("\r", " ", StringComparison.Ordinal)
            .Trim();

        return compact.Length <= 200 ? compact : compact[..200];
    }
}
