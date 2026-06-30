using GestureClip.Core.Abstractions;
using GestureClip.Core.Clipboard;
using GestureClip.Core.Settings;
using Microsoft.Extensions.Logging;

namespace GestureClip.Features.Clipboard;

public sealed class ClipboardService : IClipboardService
{
    private readonly IClipboardListener _clipboardListener;
    private readonly IClipboardTextReader _clipboardTextReader;
    private readonly IClipboardWriter _clipboardWriter;
    private readonly IClipboardRepository _clipboardRepository;
    private readonly IClipboardHashService _clipboardHashService;
    private readonly ISensitiveContentDetector _sensitiveContentDetector;
    private readonly IForegroundAppService _foregroundAppService;
    private readonly IAppBlacklistService _appBlacklistService;
    private readonly ISettingsService _settingsService;
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
        if (string.IsNullOrEmpty(item.TextContent))
        {
            return;
        }

        SuppressCaptureFor(TimeSpan.FromMilliseconds(1000));
        await _clipboardWriter.SetTextAsync(item.TextContent, cancellationToken);
        await _clipboardWriter.SendPasteHotkeyAsync(cancellationToken);
        await _clipboardRepository.IncrementUseCountAsync(item.Id, cancellationToken);
        _logger.LogInformation("Clipboard text item pasted.");
    }

    private void OnClipboardChanged(object? sender, ClipboardChangedEventArgs e)
    {
        _ = HandleClipboardChangedAsync(e);
    }

    private async Task HandleClipboardChangedAsync(ClipboardChangedEventArgs e)
    {
        try
        {
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

    private static string CreatePreview(string text)
    {
        var compact = text.Replace("\r\n", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal)
            .Replace("\r", " ", StringComparison.Ordinal)
            .Trim();

        return compact.Length <= 200 ? compact : compact[..200];
    }
}
