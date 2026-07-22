using GestureClip.Infrastructure.Clipboard;
using Xunit;

namespace GestureClip.Tests.Clipboard;

public sealed class ClipboardHtmlTextTests
{
    [Fact]
    public void ExtractPlain_strips_cf_html_fragment_markers_and_tags()
    {
        var html = """
Version:0.9
StartHTML:0000000105
EndHTML:0000000300
StartFragment:0000000140
EndFragment:0000000260
<!--StartFragment--><p>Hello <b>world</b></p><br>Second line<!--EndFragment-->
""";

        var plain = ClipboardHtmlText.ExtractPlain(html);

        Assert.NotNull(plain);
        Assert.Contains("Hello", plain, StringComparison.Ordinal);
        Assert.Contains("world", plain, StringComparison.Ordinal);
        Assert.Contains("Second line", plain, StringComparison.Ordinal);
        Assert.DoesNotContain("<b>", plain, StringComparison.Ordinal);
        Assert.DoesNotContain("StartFragment", plain, StringComparison.Ordinal);
    }

    [Fact]
    public void ExtractPlain_returns_null_for_empty()
    {
        Assert.Null(ClipboardHtmlText.ExtractPlain(null));
        Assert.Null(ClipboardHtmlText.ExtractPlain("   "));
        Assert.Null(ClipboardHtmlText.ExtractPlain("<div>   </div>"));
    }

    [Fact]
    public void ExtractPlain_decodes_entities()
    {
        var plain = ClipboardHtmlText.ExtractPlain("<!--StartFragment-->A&nbsp;&amp;&nbsp;B<!--EndFragment-->");
        Assert.NotNull(plain);
        Assert.Contains("A", plain, StringComparison.Ordinal);
        Assert.Contains("B", plain, StringComparison.Ordinal);
        Assert.Contains("&", plain, StringComparison.Ordinal);
    }
}
