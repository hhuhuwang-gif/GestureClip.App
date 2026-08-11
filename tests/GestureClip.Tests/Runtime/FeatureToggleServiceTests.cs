using GestureClip.Core.Abstractions;
using GestureClip.Core.Clipboard;
using GestureClip.Core.Gestures;
using GestureClip.Core.Runtime;
using GestureClip.Core.Settings;
using GestureClip.Features.Gestures;
using GestureClip.Features.Runtime;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GestureClip.Tests.Runtime;

public sealed class FeatureToggleServiceTests
{
    [Fact]
    public async Task SetClipboardCaptureEnabledAsync_updates_clipboard_service()
    {
        var clipboard = new FakeClipboardService();
        var service = CreateService(clipboard: clipboard);

        await service.SetClipboardCaptureEnabledAsync(false, CancellationToken.None);

        Assert.False(clipboard.IsCaptureEnabled);
    }

    [Fact]
    public async Task SetGestureEnabledAsync_updates_settings_snapshot_and_service()
    {
        var gesture = new FakeMouseGestureService();
        var edge = new FakeEdgeTriggerService();
        var settings = new FakeSettingsService();
        var provider = new FakeGestureSettingsProvider();
        var service = CreateService(gesture: gesture, edge: edge, settings: settings, provider: provider);

        await service.SetGestureEnabledAsync(false, CancellationToken.None);

        Assert.False(provider.Current.Enabled);
        Assert.Equal(false, settings.Values[SettingKeys.GestureEnabled]);
        Assert.Equal(1, gesture.StopCount);
        Assert.Equal(1, edge.StopCount);
    }

    [Fact]
    public async Task ToggleGestureAsync_uses_current_runtime_state()
    {
        var gesture = new FakeMouseGestureService { IsEnabled = false };
        var edge = new FakeEdgeTriggerService();
        var provider = new FakeGestureSettingsProvider();
        var service = CreateService(gesture: gesture, edge: edge, provider: provider);

        await service.ToggleGestureAsync(CancellationToken.None);

        Assert.True(provider.Current.Enabled);
        Assert.Equal(1, gesture.StartCount);
        Assert.Equal(1, edge.StartCount);
    }

    private static FeatureToggleService CreateService(
        FakeClipboardService? clipboard = null,
        FakeMouseGestureService? gesture = null,
        FakeEdgeTriggerService? edge = null,
        FakeSettingsService? settings = null,
        FakeGestureSettingsProvider? provider = null)
    {
        return new FeatureToggleService(
            clipboard ?? new FakeClipboardService(),
            gesture ?? new FakeMouseGestureService(),
            edge ?? new FakeEdgeTriggerService(),
            provider ?? new FakeGestureSettingsProvider(),
            settings ?? new FakeSettingsService(),
            NullLogger<FeatureToggleService>.Instance);
    }

    private sealed class FakeEdgeTriggerService : IEdgeTriggerService
    {
        public bool IsEnabled { get; private set; }
        public int StartCount { get; private set; }
        public int StopCount { get; private set; }
        public EdgeTriggerDiagnosticsSnapshot Diagnostics => new(IsEnabled, "-", "-", BuiltInGestureAction.None, "-", null, null);

        public Task StartAsync(CancellationToken cancellationToken)
        {
            IsEnabled = true;
            StartCount++;
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            IsEnabled = false;
            StopCount++;
            return Task.CompletedTask;
        }

        public void RefreshSettings() { }
    }

    private sealed class FakeClipboardService : IClipboardService
    {
        public bool IsCaptureEnabled { get; private set; } = true;
        public DateTimeOffset? SuppressCaptureUntil => null;
        public void SuppressCaptureFor(TimeSpan duration) { }
        public Task CaptureTextAsync(ClipboardCapture capture, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<ClipboardItem?> GetLatestAsync(CancellationToken cancellationToken) => Task.FromResult<ClipboardItem?>(null);
        public Task PasteAsync(ClipboardItem item, PasteOptions options, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task CopyItemsAsync(IReadOnlyList<ClipboardItem> items, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<IReadOnlyList<ClipboardItem>> SearchAsync(string keyword, int limit, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<ClipboardItem>>([]);
        public Task<int> DeleteItemsAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken) => Task.FromResult(ids.Count);
        public Task SetPinnedAsync(Guid id, bool isPinned, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task SetFavoriteAsync(Guid id, bool isFavorite, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task SetCaptureEnabledAsync(bool enabled, CancellationToken cancellationToken)
        {
            IsCaptureEnabled = enabled;
            return Task.CompletedTask;
        }

        public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakeMouseGestureService : IMouseGestureService
    {
        public bool IsEnabled { get; set; } = true;
        public int StartCount { get; private set; }
        public int StopCount { get; private set; }
        public GestureDiagnosticsSnapshot Diagnostics => new("测试", GestureRuntimeState.Idle, null, BuiltInGestureAction.None, null, null, false);

        public Task StartAsync(CancellationToken cancellationToken)
        {
            IsEnabled = true;
            StartCount++;
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            IsEnabled = false;
            StopCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeGestureSettingsProvider : IGestureSettingsProvider
    {
        public GestureSettings Current { get; private set; } =
            new(true, true, false, false, GesturePreset.EditEnhanced, new GestureOptions(20, 16, 2000, 2));

        public GestureSettings GetCurrent() => Current;

        public void Update(GestureSettings settings) => Current = settings;
    }

    private sealed class FakeSettingsService : ISettingsService
    {
        public Dictionary<string, object?> Values { get; } = [];
        public T Get<T>(string key, T defaultValue) => defaultValue;
        public Task SetAsync<T>(string key, T value, CancellationToken cancellationToken)
        {
            Values[key] = value;
            return Task.CompletedTask;
        }
    }
}
