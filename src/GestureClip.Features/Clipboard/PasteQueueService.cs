using GestureClip.Core.Abstractions;
using GestureClip.Core.Clipboard;
using GestureClip.Core.Hotkeys;
using Microsoft.Extensions.Logging;

namespace GestureClip.Features.Clipboard;

/// <summary>
/// While the queue holds items, Ctrl+V is claimed via RegisterHotKey. Each press releases the
/// hotkey, injects a normal paste of the next item, then re-claims it if items remain — so the
/// synthetic Ctrl+V never re-triggers the queue itself.
/// </summary>
public sealed class PasteQueueService : IPasteQueueService, IDisposable
{
    private static readonly HotkeyDefinition PasteHotkey =
        new(HotkeyModifier.Control, (uint)'V', "Ctrl + V");

    private readonly IHotkeyRegistrar _registrar;
    private readonly IClipboardService _clipboardService;
    private readonly ILogger<PasteQueueService> _logger;
    private readonly object _sync = new();
    private readonly Queue<ClipboardItem> _queue = new();
    private bool _hotkeyClaimed;
    private bool _pasting;

    public PasteQueueService(
        IHotkeyRegistrar registrar,
        IClipboardService clipboardService,
        ILogger<PasteQueueService> logger)
    {
        _registrar = registrar;
        _clipboardService = clipboardService;
        _logger = logger;
        _registrar.PasteQueueHotkeyPressed += OnPasteQueueHotkeyPressed;
    }

    public event EventHandler? QueueChanged;

    public int PendingCount
    {
        get
        {
            lock (_sync)
            {
                return _queue.Count;
            }
        }
    }

    public bool Enqueue(IReadOnlyList<ClipboardItem> items)
    {
        if (items.Count == 0)
        {
            return false;
        }

        bool claimed;
        lock (_sync)
        {
            foreach (var item in items)
            {
                _queue.Enqueue(item);
            }

            claimed = ClaimHotkeyLocked();
            if (!claimed)
            {
                _queue.Clear();
            }
        }

        if (!claimed)
        {
            _logger.LogWarning("Paste queue could not claim Ctrl+V hotkey. Win32Error={Win32Error}", _registrar.GetLastError());
            return false;
        }

        _logger.LogInformation("Paste queue enqueued {Count} item(s).", items.Count);
        QueueChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    public void Clear()
    {
        lock (_sync)
        {
            _queue.Clear();
            ReleaseHotkeyLocked();
        }

        _logger.LogInformation("Paste queue cleared.");
        QueueChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        _registrar.PasteQueueHotkeyPressed -= OnPasteQueueHotkeyPressed;
        Clear();
    }

    private void OnPasteQueueHotkeyPressed(object? sender, EventArgs e)
    {
        _ = PasteNextAsync();
    }

    private async Task PasteNextAsync()
    {
        ClipboardItem? item;
        lock (_sync)
        {
            if (_pasting)
            {
                return;
            }

            if (!_queue.TryDequeue(out item))
            {
                ReleaseHotkeyLocked();
                return;
            }

            _pasting = true;
            // Release Ctrl+V so the synthetic paste below reaches the target app.
            ReleaseHotkeyLocked();
        }

        try
        {
            // Let WM_HOTKEY finish and physical keys start to release before we inject input.
            await Task.Delay(60);
            await _clipboardService.PasteAsync(item, new PasteOptions(false), CancellationToken.None);
            _logger.LogInformation("Paste queue delivered one item. Remaining={Remaining}", PendingCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Paste queue item failed to paste.");
        }
        finally
        {
            lock (_sync)
            {
                _pasting = false;
                if (_queue.Count > 0)
                {
                    ClaimHotkeyLocked();
                }
            }

            QueueChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private bool ClaimHotkeyLocked()
    {
        if (_hotkeyClaimed)
        {
            return true;
        }

        _hotkeyClaimed = _registrar.RegisterPasteQueueHotkey(PasteHotkey);
        return _hotkeyClaimed;
    }

    private void ReleaseHotkeyLocked()
    {
        if (!_hotkeyClaimed)
        {
            return;
        }

        _registrar.UnregisterPasteQueueHotkey();
        _hotkeyClaimed = false;
    }
}
