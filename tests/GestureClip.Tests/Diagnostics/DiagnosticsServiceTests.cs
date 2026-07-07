using GestureClip.Core.Abstractions;
using GestureClip.Core.Gestures;
using GestureClip.Core.Hotkeys;
using GestureClip.Core.SystemInfo;
using GestureClip.Features.Diagnostics;
using GestureClip.Infrastructure.Paths;
using System.IO.Compression;
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
            new FakeKeyboardInputSender(),
            new FakeAppEnvironment());

        var report = await service.BuildReportAsync(CancellationToken.None);

        Assert.Contains("GestureClip Diagnostics", report);
        Assert.Contains(paths.DatabasePath, report);
        Assert.Contains(paths.LogDirectory, report);
        Assert.Contains("ApplicationPath:", report);
        Assert.Contains(@"C:\Program Files\GestureClip\GestureClip.exe", report);
        Assert.Contains("Ctrl + `", report);
        Assert.Contains("U", report);
        Assert.DoesNotContain("secret clipboard text", report, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExportPackageAsync_creates_zip_without_database_or_clipboard_content()
    {
        var root = Path.Combine(Path.GetTempPath(), "gestureclip-diagnostics-export", Guid.NewGuid().ToString("N"));
        var paths = new AppPathProvider(root);
        Directory.CreateDirectory(paths.LogDirectory);
        await File.WriteAllTextAsync(Path.Combine(paths.LogDirectory, "gestureclip-test.log"), "startup ok\nsecret clipboard text should not be written by app logs");
        await File.WriteAllTextAsync(paths.DatabasePath, "fake database");
        var service = new DiagnosticsService(
            paths,
            new FakePermissionService(),
            new FakeClipboardService(),
            new FakeMouseGestureService(),
            new FakeGlobalHotkeyService(),
            new FakeKeyboardInputSender(),
            new FakeAppEnvironment());

        var packagePath = await service.ExportPackageAsync(CancellationToken.None);

        Assert.True(File.Exists(packagePath));
        using var archive = ZipFile.OpenRead(packagePath);
        Assert.Contains(archive.Entries, entry => entry.FullName == "diagnostics.txt");
        Assert.Contains(archive.Entries, entry => entry.FullName.StartsWith("logs/", StringComparison.Ordinal));
        Assert.DoesNotContain(archive.Entries, entry => entry.FullName.Contains("gestureclip.db", StringComparison.OrdinalIgnoreCase));
        var reportEntry = archive.GetEntry("diagnostics.txt")!;
        using var reportReader = new StreamReader(reportEntry.Open());
        var report = await reportReader.ReadToEndAsync();
        Assert.DoesNotContain("secret clipboard text", report, StringComparison.OrdinalIgnoreCase);

        foreach (var entry in archive.Entries)
        {
            using var reader = new StreamReader(entry.Open());
            var content = await reader.ReadToEndAsync();
            Assert.DoesNotContain("secret clipboard text", content, StringComparison.OrdinalIgnoreCase);
        }
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
        public Task CopyItemsAsync(IReadOnlyList<Core.Clipboard.ClipboardItem> items, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<int> DeleteItemsAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken) => Task.FromResult(ids.Count);
        public Task SetPinnedAsync(Guid id, bool isPinned, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task SetFavoriteAsync(Guid id, bool isFavorite, CancellationToken cancellationToken) => Task.CompletedTask;
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
        public HotkeyStatus Status { get; } = new(HotkeyRegistrationState.Registered, "Ctrl + ` 已注册");
        public void Start() { }
        public void Stop() { }
    }

    private sealed class FakeKeyboardInputSender : IKeyboardInputSender
    {
        public string? LastStatus => "Sent 4/4";
        public void SendShortcut(params ushort[] keys) { }
        public void SendKey(ushort key) { }
    }

    private sealed class FakeAppEnvironment : IAppEnvironment
    {
        public string ApplicationPath => @"C:\Program Files\GestureClip\GestureClip.exe";
    }
}
