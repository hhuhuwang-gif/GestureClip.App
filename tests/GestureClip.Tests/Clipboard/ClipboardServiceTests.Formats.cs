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

    [Fact]
    public async Task Clipboard_changed_captures_and_copies_chatgpt_rich_text_body_not_image_or_preview()
    {
        var chatGptReply = string.Join("\r\n", [
            "这是 ChatGPT 回复：",
            "",
            "## 处理方案",
            "",
            "1. 保存完整正文，不保存 preview。",
            "2. 支持 Markdown、列表、多段文本、特殊 Unicode：✅ 中文 emoji Ω。",
            "",
            "```csharp",
            "public static string Normalize(string input)",
            "{",
            "    return input.Replace(\"\\r\\n\", \"\\n\");",
            "}",
            "```",
            "",
            "末尾还有一段比较长的文字，用来模拟浏览器复制回复时带来的长文本内容，避免再次复制时只写入空文本、HTML 片段或被截断的预览。"
        ]);
        var repository = new FakeClipboardRepository();
        var listener = new FakeClipboardListener();
        var writer = new FakeClipboardWriter();
        var reader = new FakeClipboardTextReader
        {
            Text = chatGptReply,
            ImagePngBase64 = "rich-html-image-snapshot"
        };
        var service = CreateService(repository, writer: writer, listener: listener, reader: reader);

        await service.StartAsync(CancellationToken.None);
        listener.Raise();
        await WaitForAsync(() => repository.Items.Count == 1);

        var saved = Assert.Single(repository.Items);
        Assert.Equal("text", saved.ContentType);
        Assert.Equal(chatGptReply, saved.TextContent);
        Assert.NotEqual(saved.PreviewText, saved.TextContent);

        repository.ReturnPreviewOnlySearchResults = true;
        var searchResults = await service.SearchAsync("Markdown", 10, CancellationToken.None);
        var selected = Assert.Single(searchResults);
        Assert.Null(selected.TextContent);
        Assert.Equal(saved.PreviewText, selected.PreviewText);

        await service.CopyItemsAsync([selected], CancellationToken.None);

        Assert.Equal(chatGptReply, writer.Text);
        Assert.Equal(1, writer.SetTextCallCount);

        await service.PasteAsync(selected, new PasteOptions(false), CancellationToken.None);

        Assert.Equal(chatGptReply, writer.Text);
        Assert.Equal(2, writer.SetTextCallCount);
        Assert.True(writer.PasteHotkeySent);
        Assert.Null(writer.ImagePngBase64);
    }

    [Fact]
    public async Task Clipboard_changed_prefers_text_when_clipboard_contains_text_and_image()
    {
        var repository = new FakeClipboardRepository();
        var listener = new FakeClipboardListener();
        var reader = new FakeClipboardTextReader
        {
            Text = "image alt text",
            ImagePngBase64 = "png-base64"
        };
        var service = CreateService(repository, listener: listener, reader: reader);

        await service.StartAsync(CancellationToken.None);
        listener.Raise();
        await WaitForAsync(() => repository.Items.Count == 1);

        Assert.Equal("text", repository.Items[0].ContentType);
        Assert.Equal("image alt text", repository.Items[0].TextContent);
    }
}
