using GestureClip.Core.Abstractions;
using GestureClip.Core.Assistant;
using GestureClip.Core.Clipboard;
using GestureClip.Core.Diagnostics;
using GestureClip.Features.Assistant;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GestureClip.Tests.Assistant;

public sealed class AssistantActionExecutorTests
{
    [Fact]
    public async Task Execute_trim_writes_clipboard_without_logging_raw_text()
    {
        var writer = new FakeClipboardWriter();
        var clipboard = new FakeClipboardService();
        var executor = CreateExecutor(clipboard, writer, textReader: new FakeClipboardTextReader("  hello  "));

        var result = await executor.ExecuteAsync(
            new AssistantActionRequest(BuiltInAssistantActionCatalog.TrimId),
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("hello", writer.Text);
        Assert.True(clipboard.SuppressCalled);
        Assert.Equal("已处理并复制到系统剪贴板。", result.Message);
        Assert.Equal(9, result.InputLength);
        Assert.Equal(5, result.OutputLength);
        Assert.DoesNotContain("hello", result.ErrorClass ?? "");
    }

    [Fact]
    public async Task Execute_json_format_fails_on_invalid_json()
    {
        var executor = CreateExecutor(textReader: new FakeClipboardTextReader("{nope"));

        var result = await executor.ExecuteAsync(
            new AssistantActionRequest(BuiltInAssistantActionCatalog.JsonFormatId),
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("invalid_json", result.ErrorClass);
        Assert.Contains("JSON", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Execute_unknown_action_fails_cleanly()
    {
        var executor = CreateExecutor();

        var result = await executor.ExecuteAsync(
            new AssistantActionRequest("missing.action"),
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("unknown_action", result.ErrorClass);
    }

    [Fact]
    public async Task Execute_open_settings_calls_lifecycle()
    {
        var lifecycle = new FakeAppLifecycleService();
        var executor = CreateExecutor(lifecycle: lifecycle);

        var result = await executor.ExecuteAsync(
            new AssistantActionRequest(BuiltInAssistantActionCatalog.OpenSettingsId),
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(1, lifecycle.ShowSettingsCount);
    }

    [Fact]
    public async Task Execute_uses_request_input_text_over_clipboard()
    {
        var writer = new FakeClipboardWriter();
        var executor = CreateExecutor(
            writer: writer,
            textReader: new FakeClipboardTextReader("from-clipboard"));

        var result = await executor.ExecuteAsync(
            new AssistantActionRequest(BuiltInAssistantActionCatalog.UpperId, InputText: "abc"),
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("ABC", writer.Text);
    }

    [Fact]
    public async Task Execute_paste_output_sends_paste_hotkey()
    {
        var writer = new FakeClipboardWriter();
        var executor = CreateExecutor(writer: writer, textReader: new FakeClipboardTextReader(" x "));

        var result = await executor.ExecuteAsync(
            new AssistantActionRequest(
                BuiltInAssistantActionCatalog.TrimId,
                OutputOverride: AssistantOutputKind.Paste),
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("x", writer.Text);
        Assert.True(writer.PasteHotkeySent);
    }

    [Fact]
    public void Catalog_only_exposes_local_only_actions()
    {
        var catalog = new BuiltInAssistantActionCatalog();
        Assert.All(catalog.GetActions(), action => Assert.Equal(AssistantPrivacyLevel.LocalOnly, action.PrivacyLevel));
        Assert.NotNull(catalog.GetById(BuiltInAssistantActionCatalog.TrimId));
    }

    private static AssistantActionExecutor CreateExecutor(
        FakeClipboardService? clipboard = null,
        FakeClipboardWriter? writer = null,
        FakeClipboardTextReader? textReader = null,
        FakeAppLifecycleService? lifecycle = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IDiagnosticsService>(new FakeDiagnosticsService());
        return new AssistantActionExecutor(
            new BuiltInAssistantActionCatalog(),
            clipboard ?? new FakeClipboardService(),
            textReader ?? new FakeClipboardTextReader(null),
            writer ?? new FakeClipboardWriter(),
            lifecycle ?? new FakeAppLifecycleService(),
            services.BuildServiceProvider(),
            NullLogger<AssistantActionExecutor>.Instance);
    }

    private sealed class FakeClipboardService : IClipboardService
    {
        public bool SuppressCalled { get; private set; }
        public bool IsCaptureEnabled => true;
        public DateTimeOffset? SuppressCaptureUntil { get; private set; }

        public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task SetCaptureEnabledAsync(bool enabled, CancellationToken cancellationToken) => Task.CompletedTask;

        public void SuppressCaptureFor(TimeSpan duration)
        {
            SuppressCalled = true;
            SuppressCaptureUntil = DateTimeOffset.UtcNow.Add(duration);
        }

        public Task CaptureTextAsync(ClipboardCapture capture, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<IReadOnlyList<ClipboardItem>> SearchAsync(string keyword, int limit, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<ClipboardItem>>([]);
        public Task<ClipboardItem?> GetLatestAsync(CancellationToken cancellationToken) => Task.FromResult<ClipboardItem?>(null);
        public Task PasteAsync(ClipboardItem item, PasteOptions options, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task CopyItemsAsync(IReadOnlyList<ClipboardItem> items, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<int> DeleteItemsAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken) => Task.FromResult(0);
        public Task SetPinnedAsync(Guid id, bool isPinned, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task SetFavoriteAsync(Guid id, bool isFavorite, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakeClipboardWriter : IClipboardWriter
    {
        public string? Text { get; private set; }
        public bool PasteHotkeySent { get; private set; }

        public Task SetTextAsync(string text, CancellationToken cancellationToken)
        {
            Text = text;
            return Task.CompletedTask;
        }

        public Task SetImagePngBase64Async(string pngBase64, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task SendPasteHotkeyAsync(CancellationToken cancellationToken)
        {
            PasteHotkeySent = true;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeClipboardTextReader(string? text) : IClipboardTextReader
    {
        public string? TryReadText() => text;
        public string? TryReadImagePngBase64() => null;
    }

    private sealed class FakeAppLifecycleService : IAppLifecycleService
    {
        public int ShowSettingsCount { get; private set; }
        public bool IsExplicitExit => false;
        public void ShowSettingsWindow(string? page = null) => ShowSettingsCount++;
        public void ToggleSettingsWindow() { }
        public void ShowWorkstationDashboardWindow() { }
        public void OpenLatestReleasePage() { }
        public Task CheckForUpdatesAsync() => Task.CompletedTask;
        public Task StartCoverUpdateAsync() => Task.CompletedTask;
        public void ExitApplication() { }
    }

    private sealed class FakeDiagnosticsService : IDiagnosticsService
    {
        public Task<DiagnosticsSnapshot> GetSnapshotAsync(CancellationToken cancellationToken)
            => Task.FromResult(new DiagnosticsSnapshot(
                "test",
                @"C:\app",
                @"C:\data\db",
                @"C:\data\logs",
                false,
                true,
                true,
                "ok",
                "ok",
                null,
                null,
                null,
                null));

        public Task<string> BuildReportAsync(CancellationToken cancellationToken) => Task.FromResult("report");
        public Task<string> ExportPackageAsync(CancellationToken cancellationToken) => Task.FromResult(@"C:\temp\diag.zip");
    }
}
