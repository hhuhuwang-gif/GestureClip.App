using GestureClip.Infrastructure.Win32;
using Xunit;

namespace GestureClip.Tests.Updates;

public sealed class KeyboardPasteTargetTests
{
    [Fact]
    public async Task Closed_explicit_target_is_rejected_before_any_keyboard_injection()
    {
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            KeyboardPasteInjector.SendCtrlVAsync(preferredTargetWindow: new IntPtr(-1)));
        Assert.Contains("目标窗口已关闭", exception.Message);
    }

    [Fact]
    public async Task Cancelled_paste_is_rejected_before_target_or_keyboard_access()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => KeyboardPasteInjector.SendCtrlVAsync(
            cancellationToken: cancellation.Token, preferredTargetWindow: new IntPtr(-1)));
    }
}
