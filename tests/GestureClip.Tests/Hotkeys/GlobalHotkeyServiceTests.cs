using GestureClip.Core.Abstractions;
using GestureClip.Core.Hotkeys;
using GestureClip.Infrastructure.Hotkeys;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GestureClip.Tests.Hotkeys;

public sealed class GlobalHotkeyServiceTests
{
    [Fact]
    public void Start_and_Stop_are_idempotent()
    {
        var registrar = new FakeHotkeyRegistrar();
        var service = CreateService(registrar);

        service.Start();
        service.Start();
        service.Stop();
        service.Stop();

        Assert.Equal(1, registrar.RegisterCount);
        Assert.Equal(1, registrar.UnregisterCount);
    }

    [Fact]
    public void Register_failure_sets_failed_status()
    {
        var registrar = new FakeHotkeyRegistrar { RegisterResult = false, LastError = 1409 };
        var service = CreateService(registrar);

        service.Start();

        Assert.Equal(HotkeyRegistrationState.Failed, service.Status.State);
        Assert.Equal(1409, service.Status.Win32Error);
    }

    [Fact]
    public async Task Hotkey_trigger_opens_clipboard_overlay()
    {
        var registrar = new FakeHotkeyRegistrar();
        var overlay = new FakeClipboardOverlayService();
        var service = CreateService(registrar, overlay);
        service.Start();

        registrar.RaiseHotkeyPressed();
        await WaitForAsync(() => overlay.ShowCount == 1);

        Assert.Equal(1, overlay.ShowCount);
    }

    private static GlobalHotkeyService CreateService(
        FakeHotkeyRegistrar registrar,
        FakeClipboardOverlayService? overlay = null)
    {
        return new GlobalHotkeyService(
            registrar,
            overlay ?? new FakeClipboardOverlayService(),
            NullLogger<GlobalHotkeyService>.Instance);
    }

    private static async Task WaitForAsync(Func<bool> condition)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(2);
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(20);
        }

        Assert.True(condition());
    }

    private sealed class FakeHotkeyRegistrar : IHotkeyRegistrar
    {
        public event EventHandler? HotkeyPressed;
        public bool RegisterResult { get; set; } = true;
        public int LastError { get; set; }
        public int RegisterCount { get; private set; }
        public int UnregisterCount { get; private set; }

        public bool RegisterOpenClipboardHotkey()
        {
            RegisterCount++;
            return RegisterResult;
        }

        public void UnregisterOpenClipboardHotkey()
        {
            UnregisterCount++;
        }

        public int GetLastError() => LastError;

        public void RaiseHotkeyPressed() => HotkeyPressed?.Invoke(this, EventArgs.Empty);
    }

    private sealed class FakeClipboardOverlayService : IClipboardOverlayService
    {
        public int ShowCount { get; private set; }

        public Task ShowAsync()
        {
            ShowCount++;
            return Task.CompletedTask;
        }
    }
}
