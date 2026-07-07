using System.Net.Http;

namespace GestureClip.Infrastructure.Updates;

public static class UpdateErrorMessageFormatter
{
    public static string ToUserMessage(Exception exception)
    {
        var raw = exception.ToString();
        if (raw.Contains("127.0.0.1:7890", StringComparison.OrdinalIgnoreCase) ||
            raw.Contains("actively refused", StringComparison.OrdinalIgnoreCase) ||
            raw.Contains("积极拒绝", StringComparison.OrdinalIgnoreCase))
        {
            return "检测到本机代理可能没有启动，程序已尝试直连重试，但仍无法访问 GitHub。请先打开代理软件，或关闭系统代理后再试。";
        }

        if (exception is HttpRequestException)
        {
            return "无法连接 GitHub。请检查网络连接后重试；也可以打开 GitHub Release 页面手动下载最新 zip。";
        }

        return "检查更新失败。请稍后再试，或打开 GitHub Release 页面手动查看。";
    }
}
