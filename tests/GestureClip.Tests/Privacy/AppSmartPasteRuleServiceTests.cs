using GestureClip.Core.SystemInfo;
using GestureClip.Features.Gestures;
using GestureClip.Features.Privacy;
using GestureClip.Tests.TestDoubles;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GestureClip.Tests.Privacy;

public sealed class AppSmartPasteRuleServiceTests
{
    [Fact]
    public async Task Set_and_try_get_roundtrip()
    {
        var settings = new FakeSettingsService();
        var sut = new AppSmartPasteRuleService(settings, NullLogger<AppSmartPasteRuleService>.Instance);

        await sut.SetAsync("Code.exe", "PlainTextPaste");
        Assert.Equal("PlainTextPaste", sut.TryGetStrategy("code.exe"));

        var all = await sut.GetAllAsync();
        Assert.Single(all);
        Assert.Equal("Code.exe", all[0].ProcessName);
    }

    [Fact]
    public void SmartPastePolicy_respects_override()
    {
        var app = new ForegroundAppInfo("chrome.exe", "Google");
        Assert.Equal(SmartPasteStrategy.CleanTextPaste, SmartPastePolicy.Select(app));
        Assert.Equal(
            SmartPasteStrategy.PlainTextPaste,
            SmartPastePolicy.Select(app, "PlainTextPaste"));
        Assert.Equal(
            SmartPasteStrategy.NormalPaste,
            SmartPastePolicy.Select(app, "NormalPaste"));
    }
}
