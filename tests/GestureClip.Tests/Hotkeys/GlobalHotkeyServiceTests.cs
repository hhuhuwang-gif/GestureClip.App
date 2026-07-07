using GestureClip.Core.Abstractions;
using GestureClip.Core.Hotkeys;
using GestureClip.Core.Settings;
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
    public void Start_registers_default_ctrl_backtick_hotkey()
    {
        var registrar = new FakeHotkeyRegistrar();
        var service = CreateService(registrar);

        service.Start();

        Assert.Equal("Ctrl + `", registrar.LastHotkeyText);
        Assert.Equal("Ctrl + ` 已注册", service.Status.DisplayText);
    }

    [Fact]
    public void Start_registers_custom_hotkey_from_settings()
    {
        var registrar = new FakeHotkeyRegistrar();
        var settings = new FakeSettingsService();
        settings.Values[SettingKeys.HotkeyOpenClipboardOverlayKey] = "Ctrl+Alt+V";
        var service = CreateService(registrar, settings: settings);

        service.Start();

        Assert.Equal("Ctrl + Alt + V", registrar.LastHotkeyText);
        Assert.Equal("Ctrl + Alt + V 已注册", service.Status.DisplayText);
    }

    [Fact]
    public void Restart_after_setting_change_registers_new_hotkey()
    {
        var registrar = new FakeHotkeyRegistrar();
        var settings = new FakeSettingsService();
        var service = CreateService(registrar, settings: settings);

        service.Start();
        settings.Values[SettingKeys.HotkeyOpenClipboardOverlayKey] = "Ctrl+Alt+V";
        service.Stop();
        service.Start();

        Assert.Equal("Ctrl + Alt + V", registrar.LastHotkeyText);
        Assert.Equal(2, registrar.RegisterCount);
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
    public void Start_falls_back_when_default_hotkey_is_already_registered()
    {
        var registrar = new FakeHotkeyRegistrar { FailedHotkeys = { "Ctrl + `" } };
        var service = CreateService(registrar);

        service.Start();

        Assert.Equal(HotkeyRegistrationState.Registered, service.Status.State);
        Assert.Equal("Ctrl + ` 被占用，已改用 Ctrl + Alt + V", service.Status.DisplayText);
        Assert.Equal(["Ctrl + `", "Ctrl + Alt + V"], registrar.RegisteredHotkeys);
    }

    [Fact]
    public async Task Hotkey_trigger_toggles_clipboard_overlay()
    {
        var registrar = new FakeHotkeyRegistrar();
        var overlay = new FakeClipboardOverlayService();
        var service = CreateService(registrar, overlay);
        service.Start();

        registrar.RaiseHotkeyPressed();
        await WaitForAsync(() => overlay.ToggleCount == 1);
        registrar.RaiseHotkeyPressed();
        await WaitForAsync(() => overlay.ToggleCount == 2);

        Assert.Equal(2, overlay.ToggleCount);
        Assert.Equal(0, overlay.ShowCount);
    }

    private static GlobalHotkeyService CreateService(
        FakeHotkeyRegistrar registrar,
        FakeClipboardOverlayService? overlay = null,
        FakeSettingsService? settings = null)
    {
        return new GlobalHotkeyService(
            registrar,
            overlay ?? new FakeClipboardOverlayService(),
            settings ?? new FakeSettingsService(),
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
        public string? LastHotkeyText { get; private set; }
        public List<string> FailedHotkeys { get; } = [];
        public List<string> RegisteredHotkeys { get; } = [];

        public bool RegisterOpenClipboardHotkey(HotkeyDefinition hotkey)
        {
            RegisterCount++;
            LastHotkeyText = hotkey.DisplayText;
            RegisteredHotkeys.Add(hotkey.DisplayText);
            if (FailedHotkeys.Contains(hotkey.DisplayText, StringComparer.Ordinal))
            {
                LastError = 1409;
                return false;
            }

            return RegisterResult;
        }

        public void UnregisterOpenClipboardHotkey()
        {
            UnregisterCount++;
        }

        public int GetLastError() => LastError;

        public void RaiseHotkeyPressed() => HotkeyPressed?.Invoke(this, EventArgs.Empty);
    }

    private sealed class FakeSettingsService : ISettingsService
    {
        public Dictionary<string, object?> Values { get; } = new();

        public T Get<T>(string key, T defaultValue) => Values.TryGetValue(key, out var value) ? (T)value! : defaultValue;

        public Task SetAsync<T>(string key, T value, CancellationToken cancellationToken)
        {
            Values[key] = value;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeClipboardOverlayService : IClipboardOverlayService
    {
        public int ShowCount { get; private set; }
        public int ToggleCount { get; private set; }
        public int RefreshCount { get; private set; }

        public Task ShowAsync()
        {
            ShowCount++;
            return Task.CompletedTask;
        }

        public Task ToggleAsync()
        {
            ToggleCount++;
            return Task.CompletedTask;
        }

        public Task RefreshAsync()
        {
            RefreshCount++;
            return Task.CompletedTask;
        }
    }
}
