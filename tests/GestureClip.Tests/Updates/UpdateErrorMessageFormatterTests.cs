using GestureClip.Infrastructure.Updates;
using Xunit;

namespace GestureClip.Tests.Updates;

public sealed class UpdateErrorMessageFormatterTests
{
    [Fact]
    public void ToUserMessage_hides_local_proxy_connection_refused_details()
    {
        var exception = new HttpRequestException("由于目标计算机积极拒绝，无法连接。 (127.0.0.1:7890)");

        var message = UpdateErrorMessageFormatter.ToUserMessage(exception);

        Assert.Contains("本机代理", message);
        Assert.Contains("直连重试", message);
        Assert.DoesNotContain("127.0.0.1:7890", message);
        Assert.DoesNotContain("积极拒绝", message);
    }
}
