using GestureClip.Core.Abstractions;
using GestureClip.Core.Clipboard;
using GestureClip.Core.Settings;
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
    private readonly IForegroundAppService _foregroundAppService;
    private readonly IAppBlacklistService _appBlacklistService;
    private readonly ISettingsService _settingsService;
    private readonly IWorkstationDashboardService _workstationDashboardService;
    private readonly ILogger<ClipboardService> _logger;
    private int _started;

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
        ILogger<ClipboardService> logger)
    {
        _clipboardListener = clipboardListener;
        _clipboardTextReader = clipboardTextReader;
        _clipboardWriter = clipboardWriter;
        _clipboardRepository = clipboardRepository;
        _clipboardHashService = clipboardHashService;
        _sensitiveContentDetector = sensitiveContentDetector;
        _foregroundAppService = foregroundAppService;
        _appBlacklistService = appBlacklistService;
        _settingsService = settingsService;
        _workstationDashboardService = workstationDashboardService;
        _logger = logger;
        IsCaptureEnabled = _settingsService.Get(SettingKeys.ClipboardCaptureEnabled, true);
    }

    public bool IsCaptureEnabled { get; private set; }

    public DateTimeOffset? SuppressCaptureUntil { get; private set; }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (Interlocked.Exchange(ref _started, 1) == 1)
        {
            return Task.CompletedTask;
        }

        _clipboardListener.ClipboardChanged += OnClipboardChanged;
        try
        {
            _clipboardListener.Start();
            _logger.LogInformation("Clipboard text history service started.");
        }
        catch
        {
            _clipboardListener.ClipboardChanged -= OnClipboardChanged;
            Interlocked.Exchange(ref _started, 0);
            throw;
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        if (Interlocked.Exchange(ref _started, 0) == 0)
        {
            return Task.CompletedTask;
        }

        _clipboardListener.ClipboardChanged -= OnClipboardChanged;
        _clipboardListener.Stop();
        _logger.LogInformation("Clipboard text history service stopped.");
        return Task.CompletedTask;
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

        if (string.IsNullOrWhiteSpace(capture.Text))
        {
            _logger.LogInformation("Clipboard capture skipped: empty text.");
            return;
        }

        var hash = _clipboardHashService.ComputeHash(capture.Text);
        var existing = await _clipboardRepository.FindByHashAsync(hash, cancellationToken);
        if (existing is not null)
        {
            _logger.LogInformation("Clipboard capture skipped: duplicate hash.");
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
            _clipboardHashService.ComputePlainTextHash(capture.Text),
            capture.SourceApp,
            capture.SourceProcess,
            false,
            false,
            isSensitive,
            0,
            now,
            now,
            null);

        await _clipboardRepository.InsertAsync(item, cancellationToken);
        await _workstationDashboardService.RecordCopyAsync(now, cancellationToken);
        _logger.LogInformation("Clipboard text item captured.");
    }

    public Task<IReadOnlyList<ClipboardItem>> SearchAsync(string keyword, int limit, CancellationToken cancellationToken)
    {
        return _clipboardRepository.SearchAsync(keyword, limit, cancellationToken);
    }

    public Task<ClipboardItem?> GetLatestAsync(CancellationToken cancellationToken)
    {
        return _clipboardRepository.GetLatestAsync(cancellationToken);
    }

    public async Task PasteAsync(ClipboardItem item, PasteOptions options, CancellationToken cancellationToken)
    {
        if (item.ContentType == "image/png")
        {
            await CopyItemsAsync([item], cancellationToken);
            await _clipboardWriter.SendPasteHotkeyAsync(cancellationToken);
            await _workstationDashboardService.RecordPasteAsync(DateTimeOffset.UtcNow, cancellationToken);
            return;
        }

        if (string.IsNullOrEmpty(item.TextContent))
        {
            return;
        }

        SuppressCaptureFor(TimeSpan.FromMilliseconds(1000));
        await _clipboardWriter.SetTextAsync(item.TextContent, cancellationToken);
        await _clipboardWriter.SendPasteHotkeyAsync(cancellationToken);
        await _clipboardRepository.IncrementUseCountAsync(item.Id, cancellationToken);
        await _workstationDashboardService.RecordPasteAsync(DateTimeOffset.UtcNow, cancellationToken);
        _logger.LogInformation("Clipboard text item pasted.");
    }

    public async Task CopyItemsAsync(IReadOnlyList<ClipboardItem> items, CancellationToken cancellationToken)
    {
        if (items.Count == 0)
        {
            return;
        }

        var hasText = items.Any(item => item.ContentType == "text");
        var hasImage = items.Any(item => item.ContentType == "image/png");
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

            var image = items[0];
            if (string.IsNullOrWhiteSpace(image.TextContent))
            {
                return;
            }

            await _clipboardWriter.SetImagePngBase64Async(image.TextContent, cancellationToken);
            await _clipboardRepository.IncrementUseCountAsync(image.Id, cancellationToken);
            _logger.LogInformation("Clipboard image item copied.");
            return;
        }

        var text = string.Join("\r\n", items.Select(item => item.TextContent).Where(text => !string.IsNullOrEmpty(text)));
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        await _clipboardWriter.SetTextAsync(text, cancellationToken);
        foreach (var item in items)
        {
            await _clipboardRepository.IncrementUseCountAsync(item.Id, cancellationToken);
        }

        _logger.LogInformation("Clipboard text items copied. Count={ClipboardItemCount}", items.Count);
    }

    public async Task<int> DeleteItemsAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken)
    {
        var deleted = await _clipboardRepository.DeleteAsync(ids, cancellationToken);
        _logger.LogInformation("Clipboard items deleted. Count={ClipboardItemCount}", deleted);
        return deleted;
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
        _ = HandleClipboardChangedAsync(e);
    }

    private async Task HandleClipboardChangedAsync(ClipboardChangedEventArgs e)
    {
        try
        {
            var imagePngBase64 = _clipboardTextReader.TryReadImagePngBase64();
            if (imagePngBase64 is not null)
            {
                await CaptureImageAsync(imagePngBase64, e.ChangedAt, CancellationToken.None);
                return;
            }

            var text = _clipboardTextReader.TryReadText();
            if (text is null)
            {
                return;
            }

            var foreground = _foregroundAppService.GetCurrent();
            await CaptureTextAsync(
                new ClipboardCapture(text, foreground.WindowTitle, foreground.ProcessName, e.ChangedAt),
                CancellationToken.None);
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

        var hash = _clipboardHashService.ComputeHash(pngBase64);
        if (await _clipboardRepository.FindByHashAsync(hash, cancellationToken) is not null)
        {
            _logger.LogInformation("Clipboard image capture skipped: duplicate hash.");
            return;
        }

        var now = DateTimeOffset.UtcNow;
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
                null),
            cancellationToken);
        await _workstationDashboardService.RecordCopyAsync(now, cancellationToken);
        _logger.LogInformation("Clipboard image item captured.");
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
