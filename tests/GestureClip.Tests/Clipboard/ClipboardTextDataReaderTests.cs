using System.Text;
using GestureClip.Infrastructure.Clipboard;
using Xunit;

namespace GestureClip.Tests.Clipboard;

public sealed class ClipboardTextDataReaderTests
{
    [Fact]
    public void TryGetText_decodes_utf8_custom_format_bytes()
    {
        var bytes = Encoding.UTF8.GetBytes("来自自定义格式的文字");

        var text = ClipboardTextDataReader.TryGetText(bytes, "UTF8_STRING");

        Assert.Equal("来自自定义格式的文字", text);
    }

    [Fact]
    public void TryGetText_detects_utf16_without_bom()
    {
        using var stream = new MemoryStream(Encoding.Unicode.GetBytes("Office 文本"));

        var text = ClipboardTextDataReader.TryGetText(stream, "UnicodeText");

        Assert.Equal("Office 文本", text);
    }

    [Fact]
    public void ClipboardRtfText_extracts_formatting_unicode_and_lines()
    {
        var plain = ClipboardRtfText.ExtractPlain(@"{\rtf1\ansi\uc1 Hello \b world\b0\par \u20320?\u22909?}");

        Assert.Equal("Hello world\n你好", plain);
    }
}
