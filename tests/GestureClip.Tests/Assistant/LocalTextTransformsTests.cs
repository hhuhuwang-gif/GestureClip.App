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
}
