using GestureClip.Features.Clipboard;
using Xunit;

namespace GestureClip.Tests.Clipboard;

public sealed class SensitiveContentDetectorTests
{
    [Theory]
    [InlineData("验证码 123456，请勿泄露")]
    [InlineData("sk-live-secret-token")]
    [InlineData("AKIAIOSFODNN7EXAMPLE")]
    [InlineData("ghp_abcdefghijklmnopqrstuvwxyz")]
    [InlineData("4111111111111111")]
    [InlineData("eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiIxMjMifQ.signature")]
    public void LooksSensitive_detects_basic_sensitive_text(string text)
    {
        var detector = new SensitiveContentDetector();

        Assert.True(detector.LooksSensitive(text));
    }

    [Fact]
    public void LooksSensitive_ignores_normal_text()
    {
        var detector = new SensitiveContentDetector();

        Assert.False(detector.LooksSensitive("normal clipboard note"));
        Assert.False(detector.LooksSensitive("123456"));
        Assert.False(detector.LooksSensitive("20260822"));
    }
}
