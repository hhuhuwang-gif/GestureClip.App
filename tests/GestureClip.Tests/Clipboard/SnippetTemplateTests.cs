using GestureClip.Features.Clipboard;
using Xunit;

namespace GestureClip.Tests.Clipboard;

public sealed class SnippetTemplateTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 26, 14, 30, 0, TimeSpan.FromHours(8));

    [Fact]
    public void Expand_replaces_known_variables()
    {
        var text = "日期 {date} 时间 {time} 全 {datetime} 年{year}月{month}日{day} {weekday}";

        var expanded = SnippetTemplate.Expand(text, Now);

        Assert.Equal("日期 2026-07-26 时间 14:30 全 2026-07-26 14:30 年2026月07日26 周日", expanded);
    }

    [Fact]
    public void Expand_is_case_insensitive_and_keeps_unknown_braces()
    {
        var expanded = SnippetTemplate.Expand("{DATE} {json} {0}", Now);

        Assert.Equal("2026-07-26 {json} {0}", expanded);
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("plain text", false)]
    [InlineData("code { block }", false)]
    [InlineData("今天是 {date}", true)]
    [InlineData("{WEEKDAY} 例会", true)]
    public void ContainsVariables_detects_only_known_variables(string? text, bool expected)
    {
        Assert.Equal(expected, SnippetTemplate.ContainsVariables(text));
    }
}
