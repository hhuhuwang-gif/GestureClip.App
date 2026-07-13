using System.Net.Http;
using System.Text;

namespace GestureClip.Infrastructure.Updates;

public static class UpdateErrorMessageFormatter
{
    public static string ToUserMessage(Exception exception)
    {
        var flattened = Flatten(exception);
        var combined = string.Join("\n", flattened.Select(ex => ex.ToString()));

        if (LooksLikeDeadLocalProxy(combined))
        {
            return
                "检测到本机代理（如 7890 端口）可能没有启动。\n" +
                "程序已自动尝试：系统代理 → 直连 → 镜像加速，仍失败。\n\n" +
                "建议：\n" +
                "1. 打开 Clash / V2Ray 等代理软件，或\n" +
                "2. 关闭系统代理后再试，或\n" +
                "3. 浏览器打开 GitHub Release 手动下载 zip 覆盖安装。";
        }

        if (LooksLikeTimeout(flattened, combined))
        {
            return
                "连接 GitHub 超时（已尝试系统代理、直连与镜像加速）。\n\n" +
                "建议检查网络，或打开 Release 页面手动下载安装包。";
        }

        if (flattened.OfType<HttpRequestException>().Any() ||
            combined.Contains("GitHub", StringComparison.OrdinalIgnoreCase) ||
            combined.Contains("镜像", StringComparison.Ordinal))
        {
            return
                "无法稳定访问 GitHub 更新服务（已自动切换系统代理 / 直连 / 镜像）。\n\n" +
                "可稍后重试，或打开 Release 页面手动下载最新 zip 覆盖安装（本地历史与设置会保留）。";
        }

        if (exception is InvalidOperationException && !string.IsNullOrWhiteSpace(exception.Message) &&
            exception is not AggregateException)
        {
            return exception.Message;
        }

        if (exception is AggregateException aggregate && !string.IsNullOrWhiteSpace(aggregate.Message))
        {
            return aggregate.Message + "\n\n可打开 GitHub Release 页面手动下载最新 zip。";
        }

        return "检查更新失败。请稍后再试，或打开 GitHub Release 页面手动查看。";
    }

    private static IReadOnlyList<Exception> Flatten(Exception exception)
    {
        if (exception is AggregateException aggregate)
        {
            return aggregate.Flatten().InnerExceptions.ToArray();
        }

        return [exception];
    }

    private static bool LooksLikeDeadLocalProxy(string raw)
    {
        if (raw.Contains("actively refused", StringComparison.OrdinalIgnoreCase) ||
            raw.Contains("积极拒绝", StringComparison.OrdinalIgnoreCase) ||
            raw.Contains("Connection refused", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Common local proxy ports mentioned in errors
        return raw.Contains("127.0.0.1:7890", StringComparison.OrdinalIgnoreCase) ||
               raw.Contains("127.0.0.1:10809", StringComparison.OrdinalIgnoreCase) ||
               raw.Contains("127.0.0.1:1080", StringComparison.OrdinalIgnoreCase) ||
               raw.Contains("127.0.0.1:7897", StringComparison.OrdinalIgnoreCase);
    }

    private static bool LooksLikeTimeout(IReadOnlyList<Exception> exceptions, string raw)
    {
        if (exceptions.Any(ex => ex is TaskCanceledException or TimeoutException ||
                                 ex.InnerException is TaskCanceledException or TimeoutException))
        {
            return true;
        }

        return raw.Contains("Timeout", StringComparison.OrdinalIgnoreCase) ||
               raw.Contains("超时", StringComparison.Ordinal);
    }
}
