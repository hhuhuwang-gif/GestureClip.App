using GestureClip.Core.Abstractions;
using GestureClip.Core.Hotkeys;
using Microsoft.Extensions.Logging;

namespace GestureClip.Infrastructure.Hotkeys;

public sealed class GlobalHotkeyService : IGlobalHotkeyService
{
    private readonly IHotkeyRegistrar _registrar;
    private readonly IClipboardOverlayService _clipboardOverlayService;
    private readonly ILogger<GlobalHotkeyService> _logger;
    private int _started;

    public GlobalHotkeyService(
        IHotkeyRegistrar registrar,
        IClipboardOverlayService clipboardOverlayService,
        ILogger<GlobalHotkeyService> logger)
    {
        _registrar = registrar;
        _clipboardOverlayService = clipboardOverlayService;
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
        if (_registrar.RegisterOpenClipboardHotkey())
        {
            Status = new HotkeyStatus(HotkeyRegistrationState.Registered, "Ctrl + Alt + V 已注册");
            _logger.LogInformation("Global hotkey registered: Ctrl+Alt+V.");
            return;
        }

        var error = _registrar.GetLastError();
        Status = new HotkeyStatus(HotkeyRegistrationState.Failed, "Ctrl + Alt + V 注册失败", error);
        _logger.LogWarning("Global hotkey registration failed. Win32Error={Win32Error}", error);
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

    private async Task ShowClipboardOverlayAsync()
    {
        try
        {
            await _clipboardOverlayService.ShowAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Global hotkey action failed.");
        }
    }
}
