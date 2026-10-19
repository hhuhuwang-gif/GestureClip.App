using GestureClip.Core.Abstractions;
using GestureClip.Core.Clipboard;
using GestureClip.Core.Settings;
using GestureClip.Core.SystemInfo;
using GestureClip.Features.Clipboard;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using GestureClip.Tests.TestDoubles;

namespace GestureClip.Tests.Clipboard;

public sealed partial class ClipboardServiceTests
{

    private sealed class FakeClipboardWriter : IClipboardWriter
    {
        public string? Text { get; private set; }
        public string? ImagePngBase64 { get; private set; }
        public bool PasteHotkeySent { get; private set; }
        public int SetTextCallCount { get; private set; }

        public Task SendPasteHotkeyAsync(CancellationToken cancellationToken)
        {
            PasteHotkeySent = true;
            return Task.CompletedTask;
        }

        public Task SetTextAsync(string text, CancellationToken cancellationToken)
        {
            SetTextCallCount++;
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
        public ClipboardReadSnapshot? Snapshot { get; set; }
        public Queue<string?> TextQueue { get; } = [];

        public string? TryReadText() => TextQueue.TryDequeue(out var text) ? text : Text;

        public string? TryReadImagePngBase64() => ImagePngBase64;

        public ClipboardReadSnapshot TryReadSnapshot()
        {
            if (Snapshot is not null)
            {
                return Snapshot;
            }

            var text = TryReadText();
            return !string.IsNullOrWhiteSpace(text)
                ? new ClipboardReadSnapshot(text, null)
                : new ClipboardReadSnapshot(null, TryReadImagePngBase64(), ImagePngBase64 is not null);
        }
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
