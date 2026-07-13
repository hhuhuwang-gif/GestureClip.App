using GestureClip.Features.Assistant;
using Xunit;

namespace GestureClip.Tests.Assistant;

public sealed class LocalTextTransformsTests
{
    [Fact]
    public void Trim_removes_leading_and_trailing_whitespace()
    {
        Assert.Equal("hello", LocalTextTransforms.Trim("  hello \n"));
    }

    [Fact]
    public void NormalizeWhitespace_collapses_runs()
    {
        Assert.Equal("a b c", LocalTextTransforms.NormalizeWhitespace(" a   b\t\tc "));
    }

    [Fact]
    public void Case_converters_work()
    {
        Assert.Equal("ABC", LocalTextTransforms.ToUpper("abc"));
        Assert.Equal("abc", LocalTextTransforms.ToLower("ABC"));
        Assert.Equal("Hello World", LocalTextTransforms.ToTitleCase("hello world"));
    }

    [Fact]
    public void Json_format_and_minify()
    {
        Assert.True(LocalTextTransforms.TryFormatJson("{\"a\":1}", out var pretty, out var error));
        Assert.Null(error);
        Assert.Contains("\n", pretty);
        Assert.Contains("\"a\"", pretty);

        Assert.True(LocalTextTransforms.TryMinifyJson(pretty, out var compact, out error));
        Assert.Null(error);
        Assert.Equal("{\"a\":1}", compact);
    }

    [Fact]
    public void Json_invalid_returns_error_class()
    {
        Assert.False(LocalTextTransforms.TryFormatJson("{bad", out _, out var error));
        Assert.Equal("invalid_json", error);
    }

    [Fact]
    public void Url_encode_decode_and_quote()
    {
        var encoded = LocalTextTransforms.UrlEncode("a b");
        Assert.Equal("a%20b", encoded);
        Assert.Equal("a b", LocalTextTransforms.UrlDecode(encoded));

        Assert.Equal("\"hi \\\"there\\\"\"", LocalTextTransforms.Quote("hi \"there\""));
        Assert.Equal("hi \"there\"", LocalTextTransforms.Unquote("\"hi \\\"there\\\"\""));
        Assert.Equal("plain", LocalTextTransforms.Unquote("'plain'"));
    }

    [Fact]
    public void CollapseBlankLines_limits_empty_rows()
    {
        Assert.Equal("a\n\nb", LocalTextTransforms.CollapseBlankLines("a\n\n\n\nb\n"));
    }

    [Fact]
    public void HtmlToPlainText_strips_tags()
    {
        var plain = LocalTextTransforms.HtmlToPlainText("<p>Hello <b>world</b></p>");
        Assert.Contains("Hello", plain);
        Assert.Contains("world", plain);
        Assert.DoesNotContain("<", plain);
    }

    [Fact]
    public void ToMarkdownLite_converts_simple_link_and_heading()
    {
        var md = LocalTextTransforms.ToMarkdownLite("<h2>Title</h2><p><a href=\"https://example.com\">Go</a></p>");
        Assert.Contains("## Title", md);
        Assert.Contains("[Go](https://example.com)", md);
    }

    [Fact]
    public void CleanTrackingUrls_removes_utm_and_spm()
    {
        var cleaned = LocalTextTransforms.CleanTrackingUrls(
            "see https://example.com/path?id=1&utm_source=tw&spm=a.b.c&keep=yes end");
        Assert.Contains("https://example.com/path?id=1&keep=yes", cleaned);
        Assert.DoesNotContain("utm_source", cleaned);
        Assert.DoesNotContain("spm=", cleaned);
    }

    [Fact]
    public void ToPlainText_handles_plain_input()
    {
        // Plain input keeps original content (no aggressive collapse).
        Assert.Equal("hi\n\n\n", LocalTextTransforms.ToPlainText("hi\n\n\n"));
    }
}
