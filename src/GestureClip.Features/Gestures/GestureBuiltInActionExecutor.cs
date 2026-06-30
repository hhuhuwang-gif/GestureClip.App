using GestureClip.Core.Abstractions;
using GestureClip.Core.Clipboard;
using GestureClip.Core.Gestures;
using GestureClip.Core.Settings;
using GestureClip.Infrastructure.Win32;
using Microsoft.Extensions.Logging;

namespace GestureClip.Features.Gestures;

public sealed class GestureBuiltInActionExecutor : IMouseGestureActionExecutor
{
    private readonly IClipboardOverlayService _clipboardOverlayService;
    private readonly IClipboardService _clipboardService;
    private readonly ISettingsService _settingsService;
    private readonly IKeyboardInputSender _keyboardInputSender;
    private readonly ILogger<GestureBuiltInActionExecutor> _logger;

    public GestureBuiltInActionExecutor(
        IClipboardOverlayService clipboardOverlayService,
        IClipboardService clipboardService,
        ISettingsService settingsService,
        IKeyboardInputSender keyboardInputSender,
        ILogger<GestureBuiltInActionExecutor> logger)
    {
        _clipboardOverlayService = clipboardOverlayService;
        _clipboardService = clipboardService;
        _settingsService = settingsService;
        _keyboardInputSender = keyboardInputSender;
        _logger = logger;
    }

    public async Task ExecuteAsync(BuiltInGestureAction action, CancellationToken cancellationToken)
    {
        switch (action)
        {
            case BuiltInGestureAction.None:
                break;

            case BuiltInGestureAction.Copy:
                _keyboardInputSender.SendShortcut(KeyboardInputNativeMethods.VkControl, KeyboardInputNativeMethods.VkC);
                break;

            case BuiltInGestureAction.Paste:
                _keyboardInputSender.SendShortcut(KeyboardInputNativeMethods.VkControl, KeyboardInputNativeMethods.VkV);
                break;

            case BuiltInGestureAction.Cut:
                _keyboardInputSender.SendShortcut(KeyboardInputNativeMethods.VkControl, KeyboardInputNativeMethods.VkX);
                break;

            case BuiltInGestureAction.SelectAll:
                _keyboardInputSender.SendShortcut(KeyboardInputNativeMethods.VkControl, KeyboardInputNativeMethods.VkA);
                break;

            case BuiltInGestureAction.Undo:
                _keyboardInputSender.SendShortcut(KeyboardInputNativeMethods.VkControl, KeyboardInputNativeMethods.VkZ);
                break;

            case BuiltInGestureAction.Redo:
                _keyboardInputSender.SendShortcut(KeyboardInputNativeMethods.VkControl, KeyboardInputNativeMethods.VkY);
                break;

            case BuiltInGestureAction.Enter:
                _keyboardInputSender.SendKey(KeyboardInputNativeMethods.VkReturn);
                break;

            case BuiltInGestureAction.Escape:
                _keyboardInputSender.SendKey(KeyboardInputNativeMethods.VkEscape);
                break;

            case BuiltInGestureAction.Delete:
                _keyboardInputSender.SendKey(KeyboardInputNativeMethods.VkDelete);
                break;

            case BuiltInGestureAction.Backspace:
                _keyboardInputSender.SendKey(KeyboardInputNativeMethods.VkBack);
                break;

            case BuiltInGestureAction.OpenClipboardOverlay:
                await _clipboardOverlayService.ShowAsync();
                break;

            case BuiltInGestureAction.PasteLatestClipboardItem:
                var latest = await _clipboardService.GetLatestAsync(cancellationToken);
                if (latest is not null)
                {
                    await _clipboardService.PasteAsync(latest, new PasteOptions(false), cancellationToken);
                }
                break;

            case BuiltInGestureAction.SendAltLeft:
                _keyboardInputSender.SendShortcut(KeyboardInputNativeMethods.VkMenu, KeyboardInputNativeMethods.VkLeft);
                break;

            case BuiltInGestureAction.SendAltRight:
                _keyboardInputSender.SendShortcut(KeyboardInputNativeMethods.VkMenu, KeyboardInputNativeMethods.VkRight);
                break;

            case BuiltInGestureAction.MinimizeForegroundWindow:
                MinimizeForegroundWindow();
                break;

            case BuiltInGestureAction.CloseForegroundWindow:
                if (_settingsService.Get(SettingKeys.GestureCloseWindowEnabled, false))
                {
                    CloseForegroundWindow();
                }
                else
                {
                    _logger.LogInformation("Close foreground window gesture is disabled.");
                }

                break;
        }
    }

    private static void MinimizeForegroundWindow()
    {
        var hwnd = WindowNativeMethods.GetForegroundWindow();
        if (hwnd != IntPtr.Zero)
        {
            WindowNativeMethods.ShowWindow(hwnd, WindowNativeMethods.SwMinimize);
        }
    }

    private static void CloseForegroundWindow()
    {
        var hwnd = WindowNativeMethods.GetForegroundWindow();
        if (hwnd != IntPtr.Zero)
        {
            WindowNativeMethods.PostMessage(hwnd, WindowNativeMethods.WmClose, IntPtr.Zero, IntPtr.Zero);
        }
    }
}
