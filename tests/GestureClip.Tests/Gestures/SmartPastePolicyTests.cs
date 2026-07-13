using GestureClip.Core.SystemInfo;
using GestureClip.Features.Gestures;
using Xunit;

namespace GestureClip.Tests.Gestures;

public sealed class SmartPastePolicyTests
{
    [Theory]
    [InlineData("WeChat.exe", SmartPasteStrategy.PlainTextPaste)]
    [InlineData("Feishu.exe", SmartPasteStrategy.PlainTextPaste)]
    [InlineData("DingTalk.exe", SmartPasteStrategy.PlainTextPaste)]
    [InlineData("Code.exe", SmartPasteStrategy.PlainTextPaste)]
    [InlineData("chrome.exe", SmartPasteStrategy.CleanTextPaste)]
    [InlineData("WINWORD.EXE", SmartPasteStrategy.NormalPaste)]
    public void Select_returns_expected_strategy(string process, SmartPasteStrategy expected)
    {
        var strategy = SmartPastePolicy.Select(new ForegroundAppInfo(process, "title"));
        Assert.Equal(expected, strategy);
    }

    [Fact]
    public void TransformForStrategy_plain_strips_html()
    {
        var result = SmartPastePolicy.TransformForStrategy("<b>x</b>", SmartPasteStrategy.PlainTextPaste);
        Assert.Equal("x", result);
    }
}
