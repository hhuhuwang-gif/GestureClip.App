using GestureClip.App.ViewModels;
using GestureClip.Core.Abstractions;
using GestureClip.Core.Assistant;
using GestureClip.Core.Clipboard;
using GestureClip.Core.Gestures;
using GestureClip.Core.Settings;
using GestureClip.Core.SystemInfo;
using GestureClip.Features.Gestures;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GestureClip.Tests.Gestures;

public sealed class SmartPasteTests
{
    [Fact]
    public void SmartPaste_is_listed_with_chinese_name_and_context_description()
    {
        var option = GestureActionCatalog.DefaultOptions.Single(item => item.Action == BuiltInGestureAction.SmartPaste);

        Assert.Equal("智能粘贴", option.Name);
        Assert.Contains("根据当前软件", option.Description, StringComparison.Ordinal);
        Assert.Contains("智能粘贴", option.DisplayName, StringComparison.Ordinal);
        Assert.Contains(GestureActionCatalog.DefaultOptions, item => item.Action == BuiltInGestureAction.Paste);
    }

    [Theory]
    [InlineData("WeChat.exe", "聊天", SmartPasteStrategy.PlainTextPaste)]
    [InlineData("Feishu.exe", "聊天", SmartPasteStrategy.PlainTextPaste)]
    [InlineData("Teams.exe", "聊天", SmartPasteStrategy.PlainTextPaste)]
    [InlineData("chrome.exe", "网页", SmartPasteStrategy.CleanTextPaste)]
    [InlineData("msedge.exe", "网页", SmartPasteStrategy.CleanTextPaste)]
    [InlineData("unknown.exe", "ChatGPT", SmartPasteStrategy.CleanTextPaste)]
    [InlineData("WINWORD.EXE", "文档", SmartPasteStrategy.NormalPaste)]
    [InlineData("EXCEL.EXE", "表格", SmartPasteStrategy.NormalPaste)]
    [InlineData("POWERPNT.EXE", "演示", SmartPasteStrategy.NormalPaste)]
    [InlineData("Code.exe", "Program.cs", SmartPasteStrategy.PlainTextPaste)]
    [InlineData("devenv.exe", "Solution", SmartPasteStrategy.PlainTextPaste)]
    [InlineData("idea64.exe", "Project", SmartPasteStrategy.PlainTextPaste)]
    [InlineData("unknown.exe", "普通窗口", SmartPasteStrategy.NormalPaste)]
    [InlineData("wechat.EXE", "大小写", SmartPasteStrategy.PlainTextPaste)]
    public void SmartPaste_policy_selects_strategy_by_foreground_app(
        string processName,
        string title,
        SmartPasteStrategy expected)
    {
        var strategy = SmartPastePolicy.Select(new ForegroundAppInfo(processName, title));

        Assert.Equal(expected, strategy);
    }

    [Theory]
    [InlineData("WeChat.exe")]
    [InlineData("Feishu.exe")]
    [InlineData("Teams.exe")]
    public async Task SmartPaste_chat_apps_write_plain_text_suppress_capture_and_send_paste(string processName)
    {
        var clipboard = new FakeClipboardService();
        var writer = new FakeClipboardWriter();
        var executor = CreateExecutor(
            new FakeKeyboardInputSender(),
            clipboard,
            new FakeClipboardTextReader { Text = "hello\r\nworld" },
            writer,
            new FakeForegroundAppService(processName, "聊天窗口"));

        await executor.ExecuteAsync(BuiltInGestureAction.SmartPaste, CancellationToken.None);

        Assert.True(clipboard.SuppressCalled);
        Assert.Equal("hello\r\nworld", writer.Text);
        Assert.True(writer.PasteHotkeySent);
        Assert.Null(writer.ImagePngBase64);
    }

    [Fact]
    public async Task SmartPaste_disabled_uses_normal_paste_without_policy_or_clipboard_rewrite()
    {
        var keyboard = new FakeKeyboardInputSender();
        var clipboard = new FakeClipboardService();
        var writer = new FakeClipboardWriter();
        var settings = new FakeSettingsService();
        settings.Values[SettingKeys.SmartPasteEnabled] = false;
        var executor = CreateExecutor(
            keyboard,
            clipboard,
            new FakeClipboardTextReader
            {
                Text = "第一行\r\n\r\n\r\n第二行"
            },
            writer,
            new FakeForegroundAppService("chrome.exe", "ChatGPT"),
            settings);

        await executor.ExecuteAsync(
            BuiltInGestureAction.SmartPaste,
            new GestureExecutionContext("D", true),
            CancellationToken.None);

        Assert.Equal(["Ctrl+V"], keyboard.Sent);
        Assert.False(clipboard.SuppressCalled);
        Assert.Null(writer.Text);
        Assert.False(writer.PasteHotkeySent);
    }

    [Theory]
    [InlineData("chrome.exe", "ChatGPT - Google Chrome")]
    [InlineData("msedge.exe", "Docs - Microsoft Edge")]
    [InlineData("unknown.exe", "ChatGPT")]
    public async Task SmartPaste_browser_or_chatgpt_writes_clean_text_not_html(string processName, string title)
    {
        var clipboard = new FakeClipboardService();
        var writer = new FakeClipboardWriter();
        var executor = CreateExecutor(
            new FakeKeyboardInputSender(),
            clipboard,
            new FakeClipboardTextReader
            {
                Text = "第一行\r\n\r\n\r\n\r\n    缩进行  \r\n第二行",
                ImagePngBase64 = "<b>html</b>"
            },
            writer,
            new FakeForegroundAppService(processName, title));

        await executor.ExecuteAsync(BuiltInGestureAction.SmartPaste, CancellationToken.None);

        Assert.True(clipboard.SuppressCalled);
        Assert.Equal("第一行\r\n\r\n    缩进行\r\n第二行", writer.Text);
        Assert.Null(writer.ImagePngBase64);
        Assert.True(writer.PasteHotkeySent);
    }

    [Theory]
    [InlineData("WINWORD.EXE")]
    [InlineData("EXCEL.EXE")]
    [InlineData("POWERPNT.EXE")]
    [InlineData("unknown.exe")]
    public async Task SmartPaste_office_and_unknown_apps_use_normal_paste_without_changing_clipboard(string processName)
    {
        var keyboard = new FakeKeyboardInputSender();
        var clipboard = new FakeClipboardService();
        var writer = new FakeClipboardWriter();
        var executor = CreateExecutor(
            keyboard,
            clipboard,
            new FakeClipboardTextReader { Text = "rich text" },
            writer,
            new FakeForegroundAppService(processName, "normal"));

        await executor.ExecuteAsync(BuiltInGestureAction.SmartPaste, CancellationToken.None);

        Assert.Equal(["Ctrl+V"], keyboard.Sent);
        Assert.False(clipboard.SuppressCalled);
        Assert.Null(writer.Text);
        Assert.False(writer.PasteHotkeySent);
    }

    [Fact]
    public async Task SmartPaste_with_left_button_modifier_forces_clean_text_even_in_office_apps()
    {
        var keyboard = new FakeKeyboardInputSender();
        var clipboard = new FakeClipboardService();
        var writer = new FakeClipboardWriter();
        var executor = CreateExecutor(
            keyboard,
            clipboard,
            new FakeClipboardTextReader { Text = "  标题  \r\n\r\n\r\n正文  " },
            writer,
            new FakeForegroundAppService("WINWORD.EXE", "文档"));

        await executor.ExecuteAsync(
            BuiltInGestureAction.SmartPaste,
            new GestureExecutionContext("D", true),
            CancellationToken.None);

        Assert.Empty(keyboard.Sent);
        Assert.True(clipboard.SuppressCalled);
        Assert.Equal("  标题\r\n\r\n正文", writer.Text);
        Assert.True(writer.PasteHotkeySent);
    }

    [Theory]
    [InlineData("Code.exe")]
    [InlineData("devenv.exe")]
    [InlineData("idea64.exe")]
    public async Task SmartPaste_code_editors_write_text_without_removing_newlines_or_indentation(string processName)
    {
        var clipboard = new FakeClipboardService();
        var writer = new FakeClipboardWriter();
        var code = "public void M()\r\n{\r\n    Console.WriteLine(\"x\");\r\n}";
        var executor = CreateExecutor(
            new FakeKeyboardInputSender(),
            clipboard,
            new FakeClipboardTextReader { Text = code },
            writer,
            new FakeForegroundAppService(processName, "Program.cs"));

        await executor.ExecuteAsync(BuiltInGestureAction.SmartPaste, CancellationToken.None);

        Assert.Equal(code, writer.Text);
        Assert.Contains("\r\n    Console", writer.Text, StringComparison.Ordinal);
        Assert.True(clipboard.SuppressCalled);
        Assert.True(writer.PasteHotkeySent);
    }

    [Fact]
    public async Task SmartPaste_without_text_falls_back_to_normal_ctrl_v()
    {
        var keyboard = new FakeKeyboardInputSender();
        var writer = new FakeClipboardWriter();
        var executor = CreateExecutor(
            keyboard,
            new FakeClipboardService(),
            new FakeClipboardTextReader { Text = null },
            writer,
            new FakeForegroundAppService("WeChat.exe", "Chat"));

        await executor.ExecuteAsync(BuiltInGestureAction.SmartPaste, CancellationToken.None);

        Assert.Null(writer.Text);
        Assert.False(writer.PasteHotkeySent);
        Assert.Equal(["Ctrl+V"], keyboard.Sent);
    }

    private static GestureBuiltInActionExecutor CreateExecutor(
        FakeKeyboardInputSender keyboard,
        FakeClipboardService clipboardService,
        FakeClipboardTextReader clipboardTextReader,
        FakeClipboardWriter clipboardWriter,
        FakeForegroundAppService foreground,
        FakeSettingsService? settings = null)
    {
        return new GestureBuiltInActionExecutor(
            new FakeClipboardOverlayService(),
            clipboardService,
            settings ?? new FakeSettingsService(),
            keyboard,
            new FakeRightClickSynthesizer(),
            new FakeCursorPositionProvider(),
            clipboardTextReader,
            clipboardWriter,
            foreground,
            new FakeUrlLauncher(),
            new FakeWorkstationDashboardService(),
            new FakeAssistantActionExecutor(),
            new FakeQuickActionCenterService(),
            new FakePlainTextPasteService(),
            NullLogger<GestureBuiltInActionExecutor>.Instance);
    }

    private sealed class FakePlainTextPasteService : IPlainTextPasteService
    {
        public Task PastePlainTextAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeKeyboardInputSender : IKeyboardInputSender
    {
        public List<string> Sent { get; } = [];
        public string? LastStatus => Sent.LastOrDefault();
        public void SendShortcut(params ushort[] keys) => Sent.Add(KeyName(keys));
        public void SendKey(ushort key) => Sent.Add(KeyName([key]));

        private static string KeyName(IReadOnlyList<ushort> keys)
        {
            return string.Join("+", keys.Select(key => key switch
            {
                0x11 => "Ctrl",
                0x56 => "V",
                _ => key.ToString()
            }));
        }
    }

    private sealed class FakeClipboardOverlayService : IClipboardOverlayService
    {
        public Task ShowAsync() => Task.CompletedTask;
        public Task ToggleAsync() => Task.CompletedTask;
        public Task RefreshAsync() => Task.CompletedTask;
    }

    private sealed class FakeClipboardService : IClipboardService
    {
        public bool IsCaptureEnabled => true;
        public DateTimeOffset? SuppressCaptureUntil => null;
        public bool SuppressCalled { get; private set; }
        public void SuppressCaptureFor(TimeSpan duration) => SuppressCalled = true;
        public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task SetCaptureEnabledAsync(bool enabled, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task CaptureTextAsync(ClipboardCapture capture, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<IReadOnlyList<ClipboardItem>> SearchAsync(string keyword, int limit, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<ClipboardItem>>([]);
        public Task<ClipboardItem?> GetLatestAsync(CancellationToken cancellationToken) => Task.FromResult<ClipboardItem?>(null);
        public Task PasteAsync(ClipboardItem item, PasteOptions options, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task CopyItemsAsync(IReadOnlyList<ClipboardItem> items, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<int> DeleteItemsAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken) => Task.FromResult(ids.Count);
        public Task SetPinnedAsync(Guid id, bool isPinned, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task SetFavoriteAsync(Guid id, bool isFavorite, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakeClipboardTextReader : IClipboardTextReader
    {
        public string? Text { get; init; }
        public string? ImagePngBase64 { get; init; }
        public string? TryReadText() => Text;
        public string? TryReadImagePngBase64() => ImagePngBase64;
    }

    private sealed class FakeClipboardWriter : IClipboardWriter
    {
        public string? Text { get; private set; }
        public string? ImagePngBase64 { get; private set; }
        public bool PasteHotkeySent { get; private set; }
        public Task SetTextAsync(string text, CancellationToken cancellationToken)
        {
            Text = text;
            return Task.CompletedTask;
        }

        public Task SetImagePngBase64Async(string pngBase64, CancellationToken cancellationToken)
        {
            ImagePngBase64 = pngBase64;
            return Task.CompletedTask;
        }

        public Task SendPasteHotkeyAsync(CancellationToken cancellationToken)
        {
            PasteHotkeySent = true;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeForegroundAppService(string? processName, string? windowTitle) : IForegroundAppService
    {
        public ForegroundAppInfo GetCurrent() => new(processName, windowTitle);
    }

    private sealed class FakeRightClickSynthesizer : IRightClickSynthesizer
    {
        public void SynthesizeRightClick(int x, int y) { }
        public void SynthesizeClick(GestureTriggerButton button, int x, int y) { }
        public void SynthesizeWheel(int delta, int x, int y) { }
    }

    private sealed class FakeCursorPositionProvider : ICursorPositionProvider
    {
        public CursorPosition GetCurrentPosition() => new(0, 0, DateTimeOffset.UtcNow);
        public ScreenBounds GetVirtualScreenBounds() => new(0, 0, 1920, 1080);
    }

    private sealed class FakeUrlLauncher : IUrlLauncher
    {
        public void OpenUrl(string url) { }
    }

    private sealed class FakeWorkstationDashboardService : IWorkstationDashboardService
    {
        public Task<Core.Workstation.WorkstationDashboardSnapshot> GetSnapshotAsync(DateTimeOffset now, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task StartFishingAsync(DateTimeOffset now, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task EndFishingAsync(DateTimeOffset now, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task ResetTodayAsync(DateOnly date, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task RecordCopyAsync(DateTimeOffset now, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task RecordPasteAsync(DateTimeOffset now, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task RecordGestureAsync(DateTimeOffset now, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakeSettingsService : ISettingsService
    {
        public Dictionary<string, object?> Values { get; } = [];
        public T Get<T>(string key, T defaultValue) => Values.TryGetValue(key, out var value) && value is T typed ? typed : defaultValue;
        public Task SetAsync<T>(string key, T value, CancellationToken cancellationToken)
        {
            Values[key] = value;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeAssistantActionExecutor : IAssistantActionExecutor
    {
        public Task<AssistantActionResult> ExecuteAsync(AssistantActionRequest request, CancellationToken cancellationToken)
            => Task.FromResult(new AssistantActionResult(true));
    }

    private sealed class FakeQuickActionCenterService : IQuickActionCenterService
    {
        public Task ShowAsync() => Task.CompletedTask;
        public Task ToggleAsync() => Task.CompletedTask;
        public Task HideAsync() => Task.CompletedTask;
    }
}
