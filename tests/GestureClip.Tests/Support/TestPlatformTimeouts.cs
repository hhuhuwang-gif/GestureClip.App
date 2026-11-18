namespace GestureClip.Tests.Support;

/// <summary>
/// 冷启动的 CI runner 首次加载 WPF 排版、字体与主题资源要花几秒，UI 线程用例的等待上限
/// 必须留足余量，否则“还没跑完”会被当成“线程死了”。本地热缓存下这些用例都在 2 秒内完成。
/// </summary>
internal static class TestPlatformTimeouts
{
    /// <summary>等待一个 STA 线程跑完 UI 用例的上限。</summary>
    public static readonly TimeSpan UiThread = TimeSpan.FromSeconds(60);
}
