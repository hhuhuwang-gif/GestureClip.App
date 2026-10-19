using System.Diagnostics;
using System.Threading.Channels;
using GestureClip.Core.Abstractions;
using GestureClip.Core.Clipboard;
using GestureClip.Core.Settings;
using Microsoft.Extensions.Logging;

namespace GestureClip.Features.Clipboard;

public sealed partial class ClipboardService : IClipboardService
{
    private const int DefaultMaxImageBytes = 5 * 1024 * 1024;
    private const int MinMaxImageBytes = 128 * 1024;
    private const int MaxMaxImageBytes = 20 * 1024 * 1024;
    /// <summary>How long an internal clipboard write fingerprint stays valid. Long enough to
    /// cover the 1.0-1.5s suppression window plus paste settle, short enough not to treat
    /// a later user copy of the same content as an echo.</summary>
    private static readonly TimeSpan InternalWriteFingerprintLifetime = TimeSpan.FromSeconds(3);
    /// <summary>Total read attempts per clipboard change event. Office, Chromium and screenshot
    /// tools may expose delayed-rendered formats or hold the clipboard for a few hundred milliseconds.</summary>
    private const int MaxClipboardReadAttempts = 6;
    private static readonly TimeSpan[] ClipboardReadRetryDelays =
    [
        TimeSpan.FromMilliseconds(15),
        TimeSpan.FromMilliseconds(30),
        TimeSpan.FromMilliseconds(60),
        TimeSpan.FromMilliseconds(100),
        TimeSpan.FromMilliseconds(150)
    ];

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
    private readonly IImageOcrService? _imageOcrService;
    private readonly ILogger<ClipboardService> _logger;
    private int _started;
    private Channel<ClipboardChangedEventArgs>? _captureQueue;
    private CancellationTokenSource? _queueCancellation;
    private Task? _queueTask;
    private readonly bool _perfLogEnabled;
    private readonly object _usageSync = new();
    private readonly Dictionary<Guid, int> _pendingUseCounts = [];
    private Task? _usageFlushTask;
    private long _historyRevision;
    private readonly object _internalWriteLock = new();
    /// <summary>Hash of the most recent content this app wrote to the system clipboard
    /// (text hash or image base64 hash). Only the hash is kept, never the content.
    /// Consumed at most once: when the WM_CLIPBOARDUPDATE echo matches, that echo is
    /// ignored; when the read content differs, the user's copy is allowed through.</summary>
    private (string? TextHash, string? ImageHash, DateTimeOffset ExpiresAt)? _pendingInternalWrite;

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
        IImageOcrService? imageOcrService = null,
        ISensitiveCaptureGate? sensitiveCaptureGate = null)
    {
        _clipboardListener = clipboardListener;
        _clipboardTextReader = clipboardTextReader;
        _clipboardWriter = clipboardWriter;
        _clipboardRepository = clipboardRepository;
        _clipboardHashService = clipboardHashService;
        _sensitiveContentDetector = sensitiveContentDetector;
        _imageOcrService = imageOcrService;
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

    public long HistoryRevision => Interlocked.Read(ref _historyRevision);

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

    /// <summary>
    /// Remember the hash of content this app just wrote to the system clipboard so its
    /// own WM_CLIPBOARDUPDATE echo can be ignored exactly once. Only the hash is stored.
    /// </summary>
    private void RecordInternalWrite(string? textHash = null, string? imageHash = null)
    {
        lock (_internalWriteLock)
        {
            _pendingInternalWrite = (textHash, imageHash, DateTimeOffset.UtcNow + InternalWriteFingerprintLifetime);
        }
    }

    /// <summary>
    /// Decide whether an incoming clipboard read is the echo of our own last write.
    /// Matching echo: skip and consume the fingerprint (at most once).
    /// Different content while the fingerprint is still live: consume the fingerprint
    /// (the old echo can no longer arrive) and let the user's copy through.
    /// No live fingerprint: fall back to the v0.6.39 time-window behavior, which is what
    /// external modules (gesture actions, assistant) rely on when they write the clipboard
    /// themselves and only call <see cref="SuppressCaptureFor"/>.
    /// </summary>
    private bool ShouldSkipAsInternalEcho(string? textHash, string? imageHash)
    {
        if (textHash is null && imageHash is null)
        {
            return false;
        }

        lock (_internalWriteLock)
        {
            if (_pendingInternalWrite is { } pending && DateTimeOffset.UtcNow <= pending.ExpiresAt)
            {
                _pendingInternalWrite = null;
                if ((textHash is not null && pending.TextHash == textHash) ||
                    (imageHash is not null && pending.ImageHash == imageHash))
                {
                    _logger.LogInformation("Clipboard capture skipped: internal write echo consumed.");
                    return true;
                }

                // Content differs from our own write: never swallow a user copy,
                // even while the old suppression window would still be open.
                return false;
            }

            _pendingInternalWrite = null;
            if (SuppressCaptureUntil is { } suppressUntil && DateTimeOffset.UtcNow < suppressUntil)
            {
                _logger.LogInformation("Clipboard capture skipped: suppressed after internal paste.");
                return true;
            }

            return false;
        }
    }

}
