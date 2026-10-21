using GestureClip.Core.Abstractions;
using GestureClip.Core.Hotkeys;
using GestureClip.Core.Settings;
using Microsoft.Extensions.Logging;

namespace GestureClip.Infrastructure.Hotkeys;

public sealed class GlobalHotkeyService : IGlobalHotkeyService
{
    private readonly IHotkeyRegistrar _registrar;
    private readonly IClipboardOverlayService _clipboardOverlayService;
    private readonly IQuickActionCenterService _quickActionCenterService;
    private readonly IPlainTextPasteService _plainTextPasteService;
    private readonly ISettingsService _settingsService;
    private readonly ILogger<GlobalHotkeyService> _logger;
    private int _started;

    public GlobalHotkeyService(
        IHotkeyRegistrar registrar,
        IClipboardOverlayService clipboardOverlayService,
        IQuickActionCenterService quickActionCenterService,
        IPlainTextPasteService plainTextPasteService,
        ISettingsService settingsService,
        ILogger<GlobalHotkeyService> logger)
    {
        _registrar = registrar;
        _clipboardOverlayService = clipboardOverlayService;
        _quickActionCenterService = quickActionCenterService;
        _plainTextPasteService = plainTextPasteService;
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
        _registrar.QuickActionHotkeyPressed += OnQuickActionHotkeyPressed;
        _registrar.PastePlainTextHotkeyPressed += OnPastePlainTextHotkeyPressed;
        RegisterClipboardHotkey();
        RegisterQuickActionHotkey();
        RegisterPastePlainTextHotkey();
    }

    public void Stop()
    {
        if (Interlocked.Exchange(ref _started, 0) == 0)
        {
            return;
        }

        _registrar.HotkeyPressed -= OnHotkeyPressed;
        _registrar.QuickActionHotkeyPressed -= OnQuickActionHotkeyPressed;
        _registrar.PastePlainTextHotkeyPressed -= OnPastePlainTextHotkeyPressed;
        _registrar.UnregisterOpenClipboardHotkey();
        _registrar.UnregisterOpenQuickActionHotkey();
        _registrar.UnregisterPastePlainTextHotkey();
        Status = new HotkeyStatus(HotkeyRegistrationState.NotStarted, "未注册");
        _logger.LogInformation("Global hotkey unregistered.");
    }

    private void RegisterClipboardHotkey()
    {
        var hotkey = HotkeyDefinition.ParseOrDefault(_settingsService.Get(
            SettingKeys.HotkeyOpenClipboardOverlayKey,
            HotkeyDefinition.DefaultOpenClipboardOverlay));

        if (TryRegisterClipboard(hotkey, null))
        {
            return;
        }

        var firstError = _registrar.GetLastError();
        var fallback = HotkeyDefinition.ParseOrDefault(HotkeyDefinition.FallbackOpenClipboardOverlay);
        if (!string.Equals(hotkey.DisplayText, fallback.DisplayText, StringComparison.Ordinal) &&
            TryRegisterClipboard(fallback, $"{hotkey.DisplayText} 被占用，已改用 {fallback.DisplayText}"))
        {
            return;
        }

        var error = _registrar.GetLastError();
        Status = new HotkeyStatus(
            HotkeyRegistrationState.Failed,
            $"{hotkey.DisplayText} 注册失败，请在剪贴板页换一个热键",
            error == 0 ? firstError : error);
        _logger.LogWarning(
            "Global hotkey registration failed. Hotkey={Hotkey} Win32Error={Win32Error}",
            hotkey.DisplayText,
            error);
    }

    private void RegisterQuickActionHotkey()
    {
        if (!_settingsService.Get(SettingKeys.HotkeyOpenQuickActionCenterEnabled, true) ||
            !_settingsService.Get(SettingKeys.AssistantEnabled, true))
        {
            return;
        }

        var hotkey = HotkeyDefinition.ParseOrDefault(_settingsService.Get(
            SettingKeys.HotkeyOpenQuickActionCenterKey,
            HotkeyDefinition.DefaultOpenQuickActionCenter));

        if (_registrar.RegisterOpenQuickActionHotkey(hotkey))
        {
            _logger.LogInformation("Quick action hotkey registered: {Hotkey}.", hotkey.DisplayText);
            return;
        }

        var fallback = HotkeyDefinition.ParseOrDefault(HotkeyDefinition.FallbackOpenQuickActionCenter);
        if (!string.Equals(hotkey.DisplayText, fallback.DisplayText, StringComparison.Ordinal) &&
            _registrar.RegisterOpenQuickActionHotkey(fallback))
        {
            _logger.LogInformation(
                "Quick action hotkey fallback registered: {Hotkey}.",
                fallback.DisplayText);
            return;
        }

        _logger.LogWarning(
            "Quick action hotkey registration failed. Hotkey={Hotkey} Win32Error={Win32Error}",
            hotkey.DisplayText,
            _registrar.GetLastError());
    }

    private void RegisterPastePlainTextHotkey()
    {
        if (!_settingsService.Get(SettingKeys.HotkeyPastePlainTextEnabled, true))
        {
            return;
        }

        if (!HotkeyDefinition.TryParse(
                _settingsService.Get(SettingKeys.HotkeyPastePlainTextKey, HotkeyDefinition.DefaultPastePlainText),
                out var hotkey))
        {
            hotkey = new HotkeyDefinition(
                HotkeyModifier.Control | HotkeyModifier.Shift,
                (uint)'V',
                HotkeyDefinition.DefaultPastePlainText);
        }

        if (_registrar.RegisterPastePlainTextHotkey(hotkey))
        {
            _logger.LogInformation("Plain text paste hotkey registered: {Hotkey}.", hotkey.DisplayText);
            return;
        }

        if (HotkeyDefinition.TryParse(HotkeyDefinition.FallbackPastePlainText, out var fallback) &&
            _registrar.RegisterPastePlainTextHotkey(fallback))
        {
            _logger.LogInformation(
                "Plain text paste hotkey fallback registered: {Hotkey}.",
                fallback.DisplayText);
            return;
        }

        _logger.LogWarning(
            "Plain text paste hotkey registration failed. Hotkey={Hotkey} Win32Error={Win32Error}",
            hotkey.DisplayText,
            _registrar.GetLastError());
    }

    private void OnHotkeyPressed(object? sender, EventArgs e)
    {
        _ = ShowClipboardOverlayAsync();
    }

    private void OnQuickActionHotkeyPressed(object? sender, EventArgs e)
    {
        _ = ToggleQuickActionCenterAsync();
    }

    private void OnPastePlainTextHotkeyPressed(object? sender, EventArgs e)
    {
        _ = PastePlainTextAsync();
    }

    private bool TryRegisterClipboard(HotkeyDefinition hotkey, string? successMessage)
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

    private async Task ToggleQuickActionCenterAsync()
    {
        try
        {
            await _quickActionCenterService.ToggleAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Quick action hotkey action failed.");
        }
    }

    private async Task PastePlainTextAsync()
    {
        try
        {
            await _plainTextPasteService.PastePlainTextAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Plain text paste hotkey action failed.");
        }
    }
}
