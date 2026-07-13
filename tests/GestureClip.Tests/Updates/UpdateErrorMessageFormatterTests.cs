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
        Assert.Contains("镜像", message);
        Assert.DoesNotContain("127.0.0.1:7890", message);
        Assert.DoesNotContain("积极拒绝", message);
    }

    [Fact]
    public void ToUserMessage_for_github_network_failure_suggests_manual_release_download_without_raw_details()
    {
        var exception = new HttpRequestException("Connection failed at 127.0.0.1:10809");

        var message = UpdateErrorMessageFormatter.ToUserMessage(exception);

        Assert.Contains("Release", message);
        Assert.Contains("手动下载", message);
        Assert.DoesNotContain("127.0.0.1:10809", message);
        Assert.DoesNotContain("Connection failed", message);
    }

    [Fact]
    public void ToUserMessage_for_aggregate_multi_path_failure_mentions_mirrors()
    {
        var exception = new AggregateException(
            "已尝试系统代理、直连与镜像加速，仍无法访问 GitHub 更新服务。",
            new HttpRequestException("timeout"),
            new TaskCanceledException("canceled"));

        var message = UpdateErrorMessageFormatter.ToUserMessage(exception);

        Assert.Contains("镜像", message);
        Assert.True(
            message.Contains("超时", StringComparison.Ordinal) ||
            message.Contains("无法", StringComparison.Ordinal));
    }
}
