using GestureClip.Core.Abstractions;
using GestureClip.Core.Hotkeys;
using GestureClip.Core.Settings;
using Microsoft.Extensions.Logging;

namespace GestureClip.Infrastructure.Hotkeys;

public sealed class GlobalHotkeyService : IGlobalHotkeyService
{
    private readonly IHotkeyRegistrar _registrar;
    private readonly IClipboardOverlayService _clipboardOverlayService;
    private readonly ISettingsService _settingsService;
    private readonly ILogger<GlobalHotkeyService> _logger;
    private int _started;

    public GlobalHotkeyService(
        IHotkeyRegistrar registrar,
        IClipboardOverlayService clipboardOverlayService,
        ISettingsService settingsService,
        ILogger<GlobalHotkeyService> logger)
    {
        _registrar = registrar;
        _clipboardOverlayService = clipboardOverlayService;
        _settingsService = settingsService;
        _logger = logger;
    }

    public HotkeyStatus Status { get; private set; } =
        new(HotkeyRegistrationState.NotStarted, "未注册");

    public void Start()
    {
        if (Interlocked.Exchange(ref _started, 1) == 1)
        {
            return;
        }

        _registrar.HotkeyPressed += OnHotkeyPressed;
        var hotkey = HotkeyDefinition.ParseOrDefault(_settingsService.Get(
            SettingKeys.HotkeyOpenClipboardOverlayKey,
            HotkeyDefinition.DefaultOpenClipboardOverlay));

        if (TryRegister(hotkey, null))
        {
            return;
        }

        var firstError = _registrar.GetLastError();
        var fallback = HotkeyDefinition.ParseOrDefault(HotkeyDefinition.FallbackOpenClipboardOverlay);
        if (!string.Equals(hotkey.DisplayText, fallback.DisplayText, StringComparison.Ordinal) &&
            TryRegister(fallback, $"{hotkey.DisplayText} 被占用，已改用 {fallback.DisplayText}"))
        {
            return;
        }

        var error = _registrar.GetLastError();
        Status = new HotkeyStatus(HotkeyRegistrationState.Failed, $"{hotkey.DisplayText} 注册失败，请在剪贴板页换一个热键", error == 0 ? firstError : error);
        _logger.LogWarning("Global hotkey registration failed. Hotkey={Hotkey} Win32Error={Win32Error}", hotkey.DisplayText, error);
    }

    public void Stop()
    {
        if (Interlocked.Exchange(ref _started, 0) == 0)
        {
            return;
        }

        _registrar.HotkeyPressed -= OnHotkeyPressed;
        _registrar.UnregisterOpenClipboardHotkey();
        Status = new HotkeyStatus(HotkeyRegistrationState.NotStarted, "未注册");
        _logger.LogInformation("Global hotkey unregistered.");
    }

    private void OnHotkeyPressed(object? sender, EventArgs e)
    {
        _ = ShowClipboardOverlayAsync();
    }

    private bool TryRegister(HotkeyDefinition hotkey, string? successMessage)
    {
        if (!_registrar.RegisterOpenClipboardHotkey(hotkey))
        {
            return false;
        }

        Status = new HotkeyStatus(HotkeyRegistrationState.Registered, successMessage ?? $"{hotkey.DisplayText} 已注册");
        _logger.LogInformation("Global hotkey registered: {Hotkey}.", hotkey.DisplayText);
        return true;
    }

    private async Task ShowClipboardOverlayAsync()
    {
        try
        {
            await _clipboardOverlayService.ToggleAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Global hotkey action failed.");
        }
    }
}
