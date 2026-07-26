using GestureClip.Features.Clipboard;
using Xunit;

namespace GestureClip.Tests.Clipboard;

public sealed class PinyinInitialMatcherTests
{
    [Theory]
    [InlineData("微信", "wx")]
    [InlineData("支付宝", "zfb")]
    [InlineData("你好世界", "nhsj")]
    [InlineData("会议纪要 2026", "hyjy")]
    public void Matches_common_chinese_words_by_pinyin_initials(string text, string initials)
    {
        Assert.True(PinyinInitialMatcher.Matches(text, initials));
    }

    [Theory]
    [InlineData("微信", "ab")]
    [InlineData("支付宝", "wx")]
    public void Does_not_match_wrong_initials(string text, string initials)
    {
        Assert.False(PinyinInitialMatcher.Matches(text, initials));
    }

    [Fact]
    public void Ascii_letters_and_digits_pass_through_lowercased()
    {
        Assert.Equal("abc123", PinyinInitialMatcher.BuildInitialSequence("A b-C…1 2#3"));
    }

    [Fact]
    public void Matches_mixed_chinese_and_ascii_text()
    {
        Assert.True(PinyinInitialMatcher.Matches("发送到微信 group", "wx"));
    }

    [Fact]
    public void Handles_null_and_empty_inputs()
    {
        Assert.False(PinyinInitialMatcher.Matches(null, "wx"));
        Assert.False(PinyinInitialMatcher.Matches("微信", null));
        Assert.False(PinyinInitialMatcher.Matches("", "wx"));
        Assert.False(PinyinInitialMatcher.Matches("微信", "  "));
    }

    [Fact]
    public void Non_cjk_characters_have_no_initial()
    {
        Assert.Null(PinyinInitialMatcher.GetInitial('a'));
        Assert.Null(PinyinInitialMatcher.GetInitial('1'));
        Assert.Null(PinyinInitialMatcher.GetInitial('!'));
    }
}
