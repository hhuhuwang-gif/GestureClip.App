using GestureClip.Core.Abstractions;
using GestureClip.Core.Clipboard;
using GestureClip.Core.Hotkeys;
using Microsoft.Extensions.Logging;

namespace GestureClip.Features.Clipboard;

public sealed partial class PasteQueueService : IPasteQueueService, IDisposable
{
    private static readonly HotkeyDefinition PasteHotkey = new(HotkeyModifier.Control, (uint)'V', "Ctrl + V");
    private readonly IHotkeyRegistrar _registrar;
    private readonly IClipboardService _clipboardService;
    private readonly IPasteTargetService _pasteTargetService;
    private readonly ILogger<PasteQueueService> _logger;
    private readonly object _sync = new();
    private readonly Queue<ClipboardItem> _queue = new();
    private bool _hotkeyClaimed;
    private bool _pasting;
    private bool _disposed;
    private long _generation;
    private string? _error;
    private CancellationTokenSource? _activePaste;

    public PasteQueueService(PasteQueueDependencies dependencies, ILogger<PasteQueueService> logger)
    {
        _registrar = dependencies.Registrar;
        _clipboardService = dependencies.ClipboardService;
        _pasteTargetService = dependencies.TargetService;
        _logger = logger;
        _registrar.PasteQueueHotkeyPressed += OnPasteQueueHotkeyPressed;
    }

    public event EventHandler? QueueChanged;
    public int PendingCount => Snapshot.PendingCount;
    public PasteQueueSnapshot Snapshot
    {
        get
        {
            lock (_sync)
            {
                var status = _queue.Count == 0 ? PasteQueueStatus.Empty :
                    _error is not null ? PasteQueueStatus.Failed :
                    _pasting ? PasteQueueStatus.Pasting : PasteQueueStatus.Ready;
                return new(_queue.Count, status, _error);
            }
        }
    }

    public bool Enqueue(IReadOnlyList<ClipboardItem> items)
    {
        if (items.Count == 0) return false;
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!_pasting && _error is null && !ClaimHotkeyLocked())
            {
                if (_queue.Count == 0) _error = null;
                return false;
            }
            foreach (var item in items) _queue.Enqueue(item);
        }
        QueueChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    public bool Retry()
    {
        bool ready;
        lock (_sync)
        {
            if (_disposed || _pasting || _queue.Count == 0 || _error is null) return false;
            _error = null;
            ready = ClaimHotkeyLocked();
        }
        QueueChanged?.Invoke(this, EventArgs.Empty);
        return ready;
    }

    public bool Skip()
    {
        lock (_sync)
        {
            if (_disposed || _pasting || _error is null || _queue.Count == 0) return false;
            _queue.Dequeue();
            _error = null;
            if (_queue.Count > 0) ClaimHotkeyLocked();
        }
        QueueChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    public void Clear()
    {
        lock (_sync)
        {
            _generation++;
            _queue.Clear();
            _error = null;
            _activePaste?.Cancel();
            ReleaseHotkeyLocked();
        }
        QueueChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        lock (_sync) _disposed = true;
        _registrar.PasteQueueHotkeyPressed -= OnPasteQueueHotkeyPressed;
        Clear();
    }

    private void OnPasteQueueHotkeyPressed(object? sender, EventArgs args) => _ = PasteNextAsync();

    private bool ClaimHotkeyLocked()
    {
        if (_hotkeyClaimed) return true;
        _hotkeyClaimed = _registrar.RegisterPasteQueueHotkey(PasteHotkey);
        if (!_hotkeyClaimed)
        {
            _error = $"Ctrl+V 注册失败，错误码 {_registrar.GetLastError()}";
            _logger.LogWarning("Paste queue hotkey unavailable: {Error}", _error);
        }
        return _hotkeyClaimed;
    }

    private void ReleaseHotkeyLocked()
    {
        if (!_hotkeyClaimed) return;
        _registrar.UnregisterPasteQueueHotkey();
        _hotkeyClaimed = false;
    }
}
