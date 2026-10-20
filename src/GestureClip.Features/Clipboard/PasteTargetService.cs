using GestureClip.Core.Abstractions;
using GestureClip.Core.SystemInfo;

namespace GestureClip.Features.Clipboard;

public sealed class PasteTargetService(IPasteTargetPlatform platform, TimeProvider timeProvider) : IPasteTargetService
{
    private static readonly TimeSpan StatePollInterval = TimeSpan.FromMilliseconds(8);

    public PasteTarget CaptureTarget() => platform.CaptureTarget();

    public async Task WaitUntilReadyAsync(PasteTarget target, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (target.WindowHandle == 0 || !platform.IsWindow(target.WindowHandle))
        {
            throw new InvalidOperationException("粘贴目标不存在，请先选中目标应用，再打开剪贴板历史。");
        }
        if (platform.ForegroundWindow != target.WindowHandle && !platform.Activate(target.WindowHandle))
        {
            throw new InvalidOperationException("无法激活粘贴目标，本次未发送输入。");
        }
        while (!platform.ArePasteKeysReleased)
        {
            EnsureTargetStillFocused(target);
            await Task.Delay(StatePollInterval, timeProvider, cancellationToken);
        }
        EnsureTargetStillFocused(target);
    }

    private void EnsureTargetStillFocused(PasteTarget target)
    {
        if (!platform.IsWindow(target.WindowHandle) || platform.ForegroundWindow != target.WindowHandle)
        {
            throw new InvalidOperationException("粘贴目标已关闭或焦点发生变化，本次未发送输入。");
        }
    }
}
