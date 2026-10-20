using GestureClip.Core.Abstractions;
using GestureClip.Core.SystemInfo;
using GestureClip.Features.Clipboard;
using Xunit;

namespace GestureClip.Tests.Clipboard;

public sealed class PasteTargetServiceTests
{
    private const int TargetHandle = 42;
    private const int OtherHandle = 99;
    private static readonly PasteTarget Target = new(TargetHandle, "notepad.exe");
    private static readonly TimeSpan TestDeadline = TimeSpan.FromSeconds(5);

    [Fact]
    public void Ready_target_completes_synchronously_without_activation_or_wait()
    {
        var platform = new TargetPlatform();
        var service = new PasteTargetService(platform, TimeProvider.System);

        var readiness = service.WaitUntilReadyAsync(Target, CancellationToken.None);

        Assert.True(readiness.IsCompletedSuccessfully);
        Assert.Equal(0, platform.ActivationCount);
    }

    [Fact]
    public async Task Activation_failure_is_reported_without_waiting_for_keys()
    {
        var platform = new TargetPlatform { ForegroundWindow = OtherHandle, ActivationSucceeds = false };
        var service = new PasteTargetService(platform, TimeProvider.System);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.WaitUntilReadyAsync(Target, CancellationToken.None));

        Assert.Contains("无法激活", error.Message);
        Assert.Equal(1, platform.ActivationCount);
        Assert.False(platform.KeysChecked.Task.IsCompleted);
    }

    [Fact]
    public async Task Closed_target_is_reported_before_activation()
    {
        var platform = new TargetPlatform { WindowExists = false };
        var service = new PasteTargetService(platform, TimeProvider.System);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.WaitUntilReadyAsync(Target, CancellationToken.None));

        Assert.Contains("粘贴目标不存在", error.Message);
        Assert.Equal(0, platform.ActivationCount);
    }

    [Fact]
    public async Task Target_closed_while_waiting_for_key_release_reports_failure()
    {
        var platform = new TargetPlatform { KeysReleased = false };
        var service = new PasteTargetService(platform, TimeProvider.System);
        var readiness = service.WaitUntilReadyAsync(Target, CancellationToken.None);
        await platform.KeysChecked.Task.WaitAsync(TestDeadline);

        platform.WindowExists = false;
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => readiness.WaitAsync(TestDeadline));

        Assert.Contains("粘贴目标已关闭或焦点发生变化", error.Message);
    }

    [Fact]
    public async Task Keys_that_remain_pressed_can_be_cancelled()
    {
        var platform = new TargetPlatform { KeysReleased = false };
        var service = new PasteTargetService(platform, TimeProvider.System);
        using var cancellation = new CancellationTokenSource();
        var readiness = service.WaitUntilReadyAsync(Target, cancellation.Token);
        await platform.KeysChecked.Task.WaitAsync(TestDeadline);
        Assert.False(readiness.IsCompleted);

        cancellation.Cancel();

        var error = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => readiness.WaitAsync(TestDeadline));
        Assert.Equal(cancellation.Token, error.CancellationToken);
        Assert.False(platform.KeysReleased);
    }

    [Fact]
    public async Task Releasing_keys_allows_pending_target_to_continue()
    {
        var platform = new TargetPlatform { KeysReleased = false };
        var service = new PasteTargetService(platform, TimeProvider.System);
        var readiness = service.WaitUntilReadyAsync(Target, CancellationToken.None);
        await platform.KeysChecked.Task.WaitAsync(TestDeadline);
        Assert.False(readiness.IsCompleted);

        platform.KeysReleased = true;
        await readiness.WaitAsync(TestDeadline);

        Assert.True(readiness.IsCompletedSuccessfully);
    }

    [Fact]
    public async Task Focus_change_during_key_release_prevents_completion()
    {
        var platform = new TargetPlatform { KeysReleased = false };
        var service = new PasteTargetService(platform, TimeProvider.System);
        var readiness = service.WaitUntilReadyAsync(Target, CancellationToken.None);
        await platform.KeysChecked.Task.WaitAsync(TestDeadline);

        platform.ForegroundWindow = OtherHandle;
        platform.KeysReleased = true;
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => readiness.WaitAsync(TestDeadline));

        Assert.Contains("焦点发生变化", error.Message);
    }

    [Fact]
    public async Task Successful_activation_rechecks_foreground_before_completing()
    {
        var platform = new TargetPlatform { ForegroundWindow = OtherHandle };
        var service = new PasteTargetService(platform, TimeProvider.System);

        await service.WaitUntilReadyAsync(Target, CancellationToken.None);

        Assert.Equal(1, platform.ActivationCount);
        Assert.Equal(Target.WindowHandle, platform.ForegroundWindow);
        Assert.Equal(Target, service.CaptureTarget());
    }

    [Fact]
    public async Task Activation_return_value_does_not_override_actual_foreground_state()
    {
        var platform = new TargetPlatform { ForegroundWindow = OtherHandle, ApplyActivationFocus = false };
        var service = new PasteTargetService(platform, TimeProvider.System);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.WaitUntilReadyAsync(Target, CancellationToken.None));

        Assert.Contains("焦点发生变化", error.Message);
        Assert.Equal(1, platform.ActivationCount);
    }

    private sealed class TargetPlatform : IPasteTargetPlatform
    {
        private nint _foregroundWindow = TargetHandle;
        private int _keysReleased = 1;
        private int _windowExists = 1;
        public bool ActivationSucceeds { get; init; } = true;
        public bool ApplyActivationFocus { get; init; } = true;
        public int ActivationCount { get; private set; }
        public TaskCompletionSource KeysChecked { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public nint ForegroundWindow
        {
            get => Volatile.Read(ref _foregroundWindow);
            set => Volatile.Write(ref _foregroundWindow, value);
        }
        public bool KeysReleased
        {
            get => Volatile.Read(ref _keysReleased) != 0;
            set => Volatile.Write(ref _keysReleased, value ? 1 : 0);
        }
        public bool WindowExists
        {
            get => Volatile.Read(ref _windowExists) != 0;
            set => Volatile.Write(ref _windowExists, value ? 1 : 0);
        }
        public bool ArePasteKeysReleased
        {
            get
            {
                KeysChecked.TrySetResult();
                return KeysReleased;
            }
        }
        public PasteTarget CaptureTarget() => Target;
        public bool IsWindow(nint windowHandle) => WindowExists && windowHandle == TargetHandle;
        public bool Activate(nint windowHandle)
        {
            ActivationCount++;
            if (ActivationSucceeds && ApplyActivationFocus) ForegroundWindow = windowHandle;
            return ActivationSucceeds;
        }
    }
}
