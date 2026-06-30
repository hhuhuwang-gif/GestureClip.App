using GestureClip.Core.Abstractions;
using GestureClip.Core.Gestures;
using GestureClip.Core.Hotkeys;
using GestureClip.Core.SystemInfo;
using GestureClip.Features.Diagnostics;
using GestureClip.Infrastructure.Paths;
using Xunit;

namespace GestureClip.Tests.Diagnostics;

public sealed class DiagnosticsServiceTests
{
    [Fact]
    public async Task BuildReportAsync_contains_status_without_clipboard_content()
    {
        var paths = new AppPathProvider(Path.Combine(Path.GetTempPath(), "gestureclip-diagnostics"));
        var service = new DiagnosticsService(
            paths,
            new FakePermissionService(),
            new FakeClipboardService(),
            new FakeMouseGestureService(),
            new FakeGlobalHotkeyService(),
            new FakeKeyboardInputSender());

        var report = await service.BuildReportAsync(CancellationToken.None);

        Assert.Contains("GestureClip Diagnostics", report);
        Assert.Contains(paths.DatabasePath, report);
        Assert.Contains(paths.LogDirectory, report);
        Assert.Contains("Ctrl+Alt+V", report);
        Assert.Contains("U", report);
        Assert.DoesNotContain("secret clipboard text", report, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class FakePermissionService : ISystemPermissionService
    {
        public PermissionStatus GetCurrentStatus() => PermissionStatus.Normal;
    }

    private sealed class FakeClipboardService : IClipboardService
    {
        public bool IsCaptureEnabled => true;
        public DateTimeOffset? SuppressCaptureUntil => null;
        public void SuppressCaptureFor(TimeSpan duration) { }
        public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task SetCaptureEnabledAsync(bool enabled, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task CaptureTextAsync(Core.Clipboard.ClipboardCapture capture, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<IReadOnlyList<Core.Clipboard.ClipboardItem>> SearchAsync(string keyword, int limit, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<Core.Clipboard.ClipboardItem>>([]);
        public Task<Core.Clipboard.ClipboardItem?> GetLatestAsync(CancellationToken cancellationToken) => Task.FromResult<Core.Clipboard.ClipboardItem?>(null);
        public Task PasteAsync(Core.Clipboard.ClipboardItem item, Core.Clipboard.PasteOptions options, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakeMouseGestureService : IMouseGestureService
    {
        public bool IsEnabled => true;
        public GestureDiagnosticsSnapshot Diagnostics => new("已安装", GestureRuntimeState.Idle, "U", BuiltInGestureAction.Copy, "secret clipboard text", DateTimeOffset.UtcNow, false);
        public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakeGlobalHotkeyService : IGlobalHotkeyService
    {
        public HotkeyStatus Status { get; } = new(HotkeyRegistrationState.Registered, "Ctrl+Alt+V 已注册");
        public void Start() { }
        public void Stop() { }
    }

    private sealed class FakeKeyboardInputSender : IKeyboardInputSender
    {
        public string? LastStatus => "Sent 4/4";
        public void SendShortcut(params ushort[] keys) { }
        public void SendKey(ushort key) { }
    }
}
