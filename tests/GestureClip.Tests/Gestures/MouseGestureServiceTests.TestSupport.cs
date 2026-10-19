using GestureClip.Core.Abstractions;
using GestureClip.Core.Gestures;
using GestureClip.Features.Gestures;
using Microsoft.Extensions.Logging.Abstractions;
using System.Collections.Concurrent;
using Xunit;

namespace GestureClip.Tests.Gestures;

public sealed partial class MouseGestureServiceTests
{
    private sealed class FakeLowLevelMouseHook : ILowLevelMouseHook
    {
        public event EventHandler<MouseHookEventArgs>? MouseEventReceived;

        public int StartCount { get; private set; }
        public int StopCount { get; private set; }

        public void Start() => StartCount++;

        public void Stop() => StopCount++;

        public MouseHookEventArgs Raise(
            MouseHookEventType type,
            int x,
            int y,
            bool isInjected = false,
            DateTimeOffset? time = null,
            int wheelDelta = 0)
        {
            var args = new MouseHookEventArgs
            {
                Event = new MouseHookEvent(type, x, y, time ?? DateTimeOffset.UtcNow, isInjected, wheelDelta)
            };
            MouseEventReceived?.Invoke(this, args);
            return args;
        }
    }

    private sealed class FakeRightClickSynthesizer : IRightClickSynthesizer
    {
        public List<(int X, int Y)> Clicks { get; } = [];
        public List<(GestureTriggerButton Button, int X, int Y)> MouseClicks { get; } = [];

        public void SynthesizeRightClick(int x, int y)
        {
            SynthesizeClick(GestureTriggerButton.Right, x, y);
        }

        public void SynthesizeClick(GestureTriggerButton button, int x, int y)
        {
            MouseClicks.Add((button, x, y));
            if (button == GestureTriggerButton.Right)
            {
                Clicks.Add((x, y));
            }
        }

        public void SynthesizeWheel(int delta, int x, int y)
        {
        }
    }

    private sealed class FakeMouseGestureActionExecutor : IMouseGestureActionExecutor
    {
        public ConcurrentQueue<BuiltInGestureAction> Actions { get; } = [];
        public ConcurrentQueue<GestureExecutionContext> Contexts { get; } = [];

        public Task ExecuteAsync(BuiltInGestureAction action, CancellationToken cancellationToken)
        {
            Actions.Enqueue(action);
            return Task.CompletedTask;
        }

        public Task ExecuteAsync(BuiltInGestureAction action, GestureExecutionContext context, CancellationToken cancellationToken)
        {
            Actions.Enqueue(action);
            Contexts.Enqueue(context);
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingMouseGestureActionExecutor : IMouseGestureActionExecutor
    {
        public Task ExecuteAsync(BuiltInGestureAction action, CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("boom");
        }
    }

    private sealed class FakeGestureSettingsProvider : IGestureSettingsProvider
    {
        private readonly bool _enabled;
        private readonly int _maxDurationMs;
        private readonly bool _showOverlay;
        private readonly bool _debugEnabled;
        private readonly bool _leftButtonEnabled;
        private readonly bool _rightButtonEnabled;
        private readonly bool _middleButtonEnabled;
        private readonly bool _xButton1Enabled;
        private readonly bool _xButton2Enabled;

        public FakeGestureSettingsProvider(bool enabled, int maxDurationMs, bool showOverlay, bool debugEnabled, bool leftButtonEnabled, bool rightButtonEnabled, bool middleButtonEnabled, bool xButton1Enabled, bool xButton2Enabled)
        {
            _enabled = enabled;
            _maxDurationMs = maxDurationMs;
            _showOverlay = showOverlay;
            _debugEnabled = debugEnabled;
            _leftButtonEnabled = leftButtonEnabled;
            _rightButtonEnabled = rightButtonEnabled;
            _middleButtonEnabled = middleButtonEnabled;
            _xButton1Enabled = xButton1Enabled;
            _xButton2Enabled = xButton2Enabled;
        }

        public GestureSettings GetCurrent()
        {
            return new GestureSettings(
                _enabled,
                _showOverlay,
                CloseWindowEnabled: false,
                DebugEnabled: _debugEnabled,
                GesturePreset.EditEnhanced,
                new GestureOptions(20, 16, _maxDurationMs, 2),
                _leftButtonEnabled,
                _middleButtonEnabled,
                _xButton1Enabled,
                _xButton2Enabled,
                _rightButtonEnabled);
        }

        public void Update(GestureSettings settings)
        {
        }
    }

    private sealed class FakeGesturePresetProvider : IGesturePresetProvider
    {
        private readonly IReadOnlyDictionary<string, BuiltInGestureAction> _bindings;

        public FakeGesturePresetProvider(IReadOnlyDictionary<string, BuiltInGestureAction> bindings)
        {
            _bindings = bindings;
        }

        public BuiltInGestureAction GetAction(GesturePreset preset, string pattern)
        {
            return _bindings.TryGetValue(pattern, out var action) ? action : BuiltInGestureAction.None;
        }

        public IReadOnlyDictionary<string, BuiltInGestureAction> GetBindings(GesturePreset preset) => _bindings;

        public void UpdateCustomBindings(IReadOnlyDictionary<string, BuiltInGestureAction> bindings)
        {
        }

        public IReadOnlyDictionary<string, BuiltInGestureAction> GetLeftButtonEnhancedBindings() =>
            new Dictionary<string, BuiltInGestureAction>(StringComparer.Ordinal);

        public void UpdateLeftButtonEnhancedBindings(IReadOnlyDictionary<string, BuiltInGestureAction> bindings)
        {
        }
    }

    private sealed class FakeForegroundAppService : IForegroundAppService
    {
        public Core.SystemInfo.ForegroundAppInfo Current { get; set; } = new("test.exe", "Test");

        public Core.SystemInfo.ForegroundAppInfo GetCurrent() => Current;
    }

    private sealed class FakeAppBlacklistService : IAppBlacklistService
    {
        public bool GestureBlocked { get; set; }

        public Task<IReadOnlyList<Core.Privacy.AppBlacklistItem>> GetAllAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<Core.Privacy.AppBlacklistItem>>([]);
        }

        public Task AddAsync(string processName, bool blockClipboard, bool blockGesture, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task DeleteAsync(Guid id, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task UpdateAsync(Guid id, bool blockClipboard, bool blockGesture, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<bool> IsClipboardBlockedAsync(string? processName, CancellationToken cancellationToken) => Task.FromResult(false);

        public Task<bool> IsGestureBlockedAsync(string? processName, CancellationToken cancellationToken) => Task.FromResult(GestureBlocked);

        public Task RefreshAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public bool IsGestureBlockedCached(string? processName) => GestureBlocked;
    }
}
