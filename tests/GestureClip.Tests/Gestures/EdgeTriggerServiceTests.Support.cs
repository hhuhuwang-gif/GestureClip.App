using GestureClip.Core.Abstractions;
using GestureClip.Core.Gestures;

namespace GestureClip.Tests.Gestures;

public sealed partial class EdgeTriggerServiceTests
{
    private sealed class FakeCursorPositionProvider : ICursorPositionProvider
    {
        public CursorPosition Position { get; set; } = new(100, 100, DateTimeOffset.UtcNow);
        public ScreenBounds Bounds { get; set; } = new(0, 0, 1920, 1080);

        public CursorPosition GetCurrentPosition() => Position;

        public ScreenBounds GetVirtualScreenBounds() => Bounds;
    }

    private sealed class FakeGestureActionExecutor : IMouseGestureActionExecutor
    {
        public List<BuiltInGestureAction> Actions { get; } = [];

        public Task ExecuteAsync(BuiltInGestureAction action, CancellationToken cancellationToken)
        {
            Actions.Add(action);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeLowLevelMouseHook : ILowLevelMouseHook
    {
        public event EventHandler<MouseHookEventArgs>? MouseEventReceived;
        public int StartCount { get; private set; }
        public int StopCount { get; private set; }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            StartCount++;
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            StopCount++;
            return Task.CompletedTask;
        }

        public MouseHookEventArgs Emit(MouseHookEvent mouseEvent)
        {
            var args = new MouseHookEventArgs { Event = mouseEvent };
            MouseEventReceived?.Invoke(this, args);
            return args;
        }
    }

    private sealed class FakeSettingsService : ISettingsService
    {
        public Dictionary<string, object?> Values { get; } = [];
        public int GetCount { get; set; }

        public T Get<T>(string key, T defaultValue)
        {
            GetCount++;
            return Values.TryGetValue(key, out var value) ? (T)value! : defaultValue;
        }

        public Task SetAsync<T>(string key, T value, CancellationToken cancellationToken)
        {
            Values[key] = value;
            return Task.CompletedTask;
        }
    }
}
