using GestureClip.Core.Abstractions;
using GestureClip.Core.Clipboard;
using GestureClip.Core.Gestures;
using GestureClip.Core.Settings;
using GestureClip.Infrastructure.Win32;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

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

            case BuiltInGestureAction.PasteAndEnter:
                _keyboardInputSender.SendShortcut(KeyboardInputNativeMethods.VkControl, KeyboardInputNativeMethods.VkV);
                _keyboardInputSender.SendKey(KeyboardInputNativeMethods.VkReturn);
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

            case BuiltInGestureAction.NewTab:
                _keyboardInputSender.SendShortcut(KeyboardInputNativeMethods.VkControl, KeyboardInputNativeMethods.VkT);
                break;

            case BuiltInGestureAction.ReopenClosedTab:
                _keyboardInputSender.SendShortcut(KeyboardInputNativeMethods.VkControl, KeyboardInputNativeMethods.VkShift, KeyboardInputNativeMethods.VkT);
                break;

            case BuiltInGestureAction.Refresh:
                _keyboardInputSender.SendKey(KeyboardInputNativeMethods.VkF5);
                break;

            case BuiltInGestureAction.CloseTab:
                _keyboardInputSender.SendShortcut(KeyboardInputNativeMethods.VkControl, KeyboardInputNativeMethods.VkW);
                break;

            case BuiltInGestureAction.StartMenu:
                _keyboardInputSender.SendKey(KeyboardInputNativeMethods.VkLWin);
                break;

            case BuiltInGestureAction.ShowDesktop:
                _keyboardInputSender.SendShortcut(KeyboardInputNativeMethods.VkLWin, KeyboardInputNativeMethods.VkD);
                break;

            case BuiltInGestureAction.SwitchApp:
                _keyboardInputSender.SendShortcut(KeyboardInputNativeMethods.VkMenu, KeyboardInputNativeMethods.VkTab);
                break;

            case BuiltInGestureAction.TaskSwitcher:
                _keyboardInputSender.SendShortcut(KeyboardInputNativeMethods.VkControl, KeyboardInputNativeMethods.VkMenu, KeyboardInputNativeMethods.VkTab);
                break;

            case BuiltInGestureAction.PlayPause:
                _keyboardInputSender.SendKey(KeyboardInputNativeMethods.VkMediaPlayPause);
                break;

            case BuiltInGestureAction.VolumeUp:
                _keyboardInputSender.SendKey(KeyboardInputNativeMethods.VkVolumeUp);
                break;

            case BuiltInGestureAction.VolumeDown:
                _keyboardInputSender.SendKey(KeyboardInputNativeMethods.VkVolumeDown);
                break;

            case BuiltInGestureAction.Mute:
                _keyboardInputSender.SendKey(KeyboardInputNativeMethods.VkVolumeMute);
                break;

            case BuiltInGestureAction.PreviousTrack:
                _keyboardInputSender.SendKey(KeyboardInputNativeMethods.VkMediaPrevTrack);
                break;

            case BuiltInGestureAction.NextTrack:
                _keyboardInputSender.SendKey(KeyboardInputNativeMethods.VkMediaNextTrack);
                break;

            case BuiltInGestureAction.TaskManager:
                StartProcess("taskmgr.exe");
                break;

            case BuiltInGestureAction.SystemSettings:
                StartProcess("ms-settings:");
                break;

            case BuiltInGestureAction.Sleep:
                StartProcess("rundll32.exe", "powrprof.dll,SetSuspendState 0,1,0");
                break;

            case BuiltInGestureAction.ZoomIn:
                _keyboardInputSender.SendShortcut(KeyboardInputNativeMethods.VkControl, KeyboardInputNativeMethods.VkOemPlus);
                break;

            case BuiltInGestureAction.ZoomOut:
                _keyboardInputSender.SendShortcut(KeyboardInputNativeMethods.VkControl, KeyboardInputNativeMethods.VkOemMinus);
                break;

            case BuiltInGestureAction.ResetZoom:
                _keyboardInputSender.SendShortcut(KeyboardInputNativeMethods.VkControl, KeyboardInputNativeMethods.Vk0);
                break;

            case BuiltInGestureAction.Home:
                _keyboardInputSender.SendKey(KeyboardInputNativeMethods.VkHome);
                break;

            case BuiltInGestureAction.End:
                _keyboardInputSender.SendKey(KeyboardInputNativeMethods.VkEnd);
                break;

            case BuiltInGestureAction.PageUp:
                _keyboardInputSender.SendKey(KeyboardInputNativeMethods.VkPageUp);
                break;

            case BuiltInGestureAction.PageDown:
                _keyboardInputSender.SendKey(KeyboardInputNativeMethods.VkPageDown);
                break;

            case BuiltInGestureAction.Screenshot:
                _keyboardInputSender.SendShortcut(KeyboardInputNativeMethods.VkLWin, KeyboardInputNativeMethods.VkShift, KeyboardInputNativeMethods.VkS);
                break;

            case BuiltInGestureAction.NextVirtualDesktop:
                _keyboardInputSender.SendShortcut(KeyboardInputNativeMethods.VkControl, KeyboardInputNativeMethods.VkLWin, KeyboardInputNativeMethods.VkRight);
                break;

            case BuiltInGestureAction.PreviousVirtualDesktop:
                _keyboardInputSender.SendShortcut(KeyboardInputNativeMethods.VkControl, KeyboardInputNativeMethods.VkLWin, KeyboardInputNativeMethods.VkLeft);
                break;

            case BuiltInGestureAction.FullScreen:
                _keyboardInputSender.SendKey(KeyboardInputNativeMethods.VkF11);
                break;

            case BuiltInGestureAction.PinWindow:
                _logger.LogInformation("PinWindow gesture action is reserved and not executed in this build.");
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

    private void StartProcess(string fileName, string? arguments = null)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments ?? "",
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to start process for gesture action. FileName={FileName}", fileName);
        }
    }
}
