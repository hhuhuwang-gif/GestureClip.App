using GestureClip.Core.Abstractions;
using GestureClip.Core.Assistant;
using GestureClip.Core.Clipboard;
using GestureClip.Core.Gestures;
using GestureClip.Core.Settings;
using GestureClip.Features.Assistant;
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
    private readonly IRightClickSynthesizer _mouseClickSynthesizer;
    private readonly ICursorPositionProvider _cursorPositionProvider;
    private readonly IClipboardTextReader _clipboardTextReader;
    private readonly IClipboardWriter _clipboardWriter;
    private readonly IForegroundAppService _foregroundAppService;
    private readonly IUrlLauncher _urlLauncher;
    private readonly IWorkstationDashboardService _workstationDashboardService;
    private readonly IAssistantActionExecutor _assistantActionExecutor;
    private readonly IQuickActionCenterService _quickActionCenterService;
    private readonly IPlainTextPasteService _plainTextPasteService;
    private readonly ILogger<GestureBuiltInActionExecutor> _logger;

    public GestureBuiltInActionExecutor(
        IClipboardOverlayService clipboardOverlayService,
        IClipboardService clipboardService,
        ISettingsService settingsService,
        IKeyboardInputSender keyboardInputSender,
        IRightClickSynthesizer mouseClickSynthesizer,
        ICursorPositionProvider cursorPositionProvider,
        IClipboardTextReader clipboardTextReader,
        IClipboardWriter clipboardWriter,
        IForegroundAppService foregroundAppService,
        IUrlLauncher urlLauncher,
        IWorkstationDashboardService workstationDashboardService,
        IAssistantActionExecutor assistantActionExecutor,
        IQuickActionCenterService quickActionCenterService,
        IPlainTextPasteService plainTextPasteService,
        ILogger<GestureBuiltInActionExecutor> logger)
    {
        _clipboardOverlayService = clipboardOverlayService;
        _clipboardService = clipboardService;
        _settingsService = settingsService;
        _keyboardInputSender = keyboardInputSender;
        _mouseClickSynthesizer = mouseClickSynthesizer;
        _cursorPositionProvider = cursorPositionProvider;
        _clipboardTextReader = clipboardTextReader;
        _clipboardWriter = clipboardWriter;
        _foregroundAppService = foregroundAppService;
        _urlLauncher = urlLauncher;
        _workstationDashboardService = workstationDashboardService;
        _assistantActionExecutor = assistantActionExecutor;
        _quickActionCenterService = quickActionCenterService;
        _plainTextPasteService = plainTextPasteService;
        _logger = logger;
    }

    public async Task ExecuteAsync(BuiltInGestureAction action, CancellationToken cancellationToken)
    {
        await ExecuteAsync(action, new GestureExecutionContext("", false), cancellationToken);
    }

    public async Task ExecuteAsync(
        BuiltInGestureAction action,
        GestureExecutionContext context,
        CancellationToken cancellationToken)
    {
        switch (action)
        {
            case BuiltInGestureAction.None:
                break;

            case BuiltInGestureAction.Copy:
                _keyboardInputSender.SendShortcut(KeyboardInputNativeMethods.VkControl, KeyboardInputNativeMethods.VkC);
                break;

            case BuiltInGestureAction.Paste:
                // A1: when smart paste is on, ordinary paste also respects app policy.
                await ExecuteSmartPasteAsync(context, cancellationToken, forceCleanWhenLeftModified: false, allowNormalShortcut: true);
                break;

            case BuiltInGestureAction.SmartPaste:
                await ExecuteSmartPasteAsync(context, cancellationToken, forceCleanWhenLeftModified: true, allowNormalShortcut: true);
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
                await ExecuteSmartPasteAsync(context, cancellationToken, forceCleanWhenLeftModified: false, allowNormalShortcut: true);
                _keyboardInputSender.SendKey(KeyboardInputNativeMethods.VkReturn);
                break;

            case BuiltInGestureAction.OpenClipboardOverlay:
                await _clipboardOverlayService.ShowAsync();
                break;

            case BuiltInGestureAction.OpenQuickActionCenter:
                await _quickActionCenterService.ShowAsync();
                break;

            case BuiltInGestureAction.AssistantTrim:
            case BuiltInGestureAction.AssistantNormalizeWhitespace:
            case BuiltInGestureAction.AssistantCollapseBlankLines:
            case BuiltInGestureAction.AssistantUpper:
            case BuiltInGestureAction.AssistantLower:
            case BuiltInGestureAction.AssistantTitleCase:
            case BuiltInGestureAction.AssistantJsonFormat:
            case BuiltInGestureAction.AssistantJsonMinify:
            case BuiltInGestureAction.AssistantUrlEncode:
            case BuiltInGestureAction.AssistantUrlDecode:
            case BuiltInGestureAction.AssistantQuote:
            case BuiltInGestureAction.AssistantUnquote:
            case BuiltInGestureAction.AssistantPlainText:
            case BuiltInGestureAction.AssistantHtmlToText:
            case BuiltInGestureAction.AssistantToMarkdown:
            case BuiltInGestureAction.AssistantCleanUrl:
                await ExecuteAssistantActionAsync(action, cancellationToken);
                break;

            case BuiltInGestureAction.PastePlainText:
                await _plainTextPasteService.PastePlainTextAsync(cancellationToken);
                await RecordPasteAsync(cancellationToken);
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

            case BuiltInGestureAction.NextTab:
                _keyboardInputSender.SendShortcut(KeyboardInputNativeMethods.VkControl, KeyboardInputNativeMethods.VkTab);
                break;

            case BuiltInGestureAction.PreviousTab:
                _keyboardInputSender.SendShortcut(KeyboardInputNativeMethods.VkControl, KeyboardInputNativeMethods.VkShift, KeyboardInputNativeMethods.VkTab);
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

            case BuiltInGestureAction.LeftMouseClick:
                SynthesizeMouseClick(GestureTriggerButton.Left);
                break;

            case BuiltInGestureAction.LeftMouseDoubleClick:
                SynthesizeMouseClick(GestureTriggerButton.Left);
                SynthesizeMouseClick(GestureTriggerButton.Left);
                break;

            case BuiltInGestureAction.RightMouseClick:
                SynthesizeMouseClick(GestureTriggerButton.Right);
                break;

            case BuiltInGestureAction.MiddleMouseClick:
                SynthesizeMouseClick(GestureTriggerButton.Middle);
                break;

            case BuiltInGestureAction.MouseWheelUp:
                SynthesizeMouseWheel(1);
                break;

            case BuiltInGestureAction.MouseWheelDown:
                SynthesizeMouseWheel(-1);
                break;

            case BuiltInGestureAction.SearchSelectedTextWithGoogle:
                await SearchSelectedTextAsync("https://www.google.com/search?q={0}", cancellationToken);
                break;

            case BuiltInGestureAction.SearchSelectedTextWithBaidu:
                await SearchSelectedTextAsync("https://www.baidu.com/s?wd={0}", cancellationToken);
                break;

            case BuiltInGestureAction.SearchSelectedTextWithBing:
                await SearchSelectedTextAsync("https://www.bing.com/search?q={0}", cancellationToken);
                break;

            case BuiltInGestureAction.OpenGoogle:
                _urlLauncher.OpenUrl("https://www.google.com/");
                break;

            case BuiltInGestureAction.OpenBaidu:
                _urlLauncher.OpenUrl("https://www.baidu.com/");
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

    private void SynthesizeMouseClick(GestureTriggerButton button)
    {
        var position = _cursorPositionProvider.GetCurrentPosition();
        _mouseClickSynthesizer.SynthesizeClick(button, position.X, position.Y);
    }

    private void SynthesizeMouseWheel(int delta)
    {
        var position = _cursorPositionProvider.GetCurrentPosition();
        _mouseClickSynthesizer.SynthesizeWheel(delta, position.X, position.Y);
    }

    private async Task RecordPasteAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _workstationDashboardService.RecordPasteAsync(DateTimeOffset.UtcNow, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Gesture paste stats recording failed.");
        }
    }

    private async Task ExecuteSmartPasteAsync(
        GestureExecutionContext context,
        CancellationToken cancellationToken,
        bool forceCleanWhenLeftModified,
        bool allowNormalShortcut)
    {
        // Right-button gesture settle only. Do not steal focus before we know we need it.
        await Task.Delay(35, cancellationToken);

        // Smart paste = optional clipboard rewrite ONLY.
        // Injection MUST always use the same path as "smart paste off" (keyboard Ctrl+V).
        // Previous design used a separate SendPasteHotkeyAsync path and early-return; when that
        // path reported success without actually pasting, gesture paste was dead while
        // "disable smart paste" still worked.
        if (_settingsService.Get(SettingKeys.SmartPasteEnabled, true))
        {
            await TryPrepareSmartClipboardAsync(context, forceCleanWhenLeftModified, cancellationToken);
        }
        else
        {
            _logger.LogInformation("Smart paste is disabled. Using normal paste only.");
        }

        if (allowNormalShortcut)
        {
            await SendNormalPasteAsync(context, cancellationToken);
        }
    }

    /// <summary>
    /// Best-effort rewrite of system clipboard for plain/clean strategies.
    /// Never throws out; never injects keys. Failure must not block normal paste.
    /// </summary>
    private async Task TryPrepareSmartClipboardAsync(
        GestureExecutionContext context,
        bool forceCleanWhenLeftModified,
        CancellationToken cancellationToken)
    {
        try
        {
            var app = _foregroundAppService.GetCurrent();
            var strategy = forceCleanWhenLeftModified && context.IsLeftButtonModified
                ? SmartPasteStrategy.CleanTextPaste
                : SmartPastePolicy.Select(app);

            if (strategy is not (SmartPasteStrategy.PlainTextPaste or SmartPasteStrategy.CleanTextPaste))
            {
                _logger.LogDebug(
                    "Smart paste skip rewrite (NormalPaste). Process={ProcessName}",
                    app.ProcessName);
                return;
            }

            var text = _clipboardTextReader.TryReadText();
            if (string.IsNullOrEmpty(text))
            {
                _logger.LogInformation(
                    "Smart paste has no text; skip rewrite. Process={ProcessName}",
                    app.ProcessName);
                return;
            }

            var pasteText = SmartPastePolicy.TransformForStrategy(text, strategy);
            if (string.IsNullOrEmpty(pasteText))
            {
                _logger.LogInformation(
                    "Smart paste transform empty; keep original clipboard. Process={ProcessName}",
                    app.ProcessName);
                return;
            }

            // No-op rewrite: avoid OpenClipboard churn that can race paste on some PCs.
            if (string.Equals(pasteText, text, StringComparison.Ordinal))
            {
                _logger.LogDebug(
                    "Smart paste transform unchanged; skip rewrite. Process={ProcessName}",
                    app.ProcessName);
                return;
            }

            _clipboardService.SuppressCaptureFor(TimeSpan.FromMilliseconds(1500));
            await _clipboardWriter.SetTextAsync(pasteText, cancellationToken);
            // Clipboard ownership / viewer notify must settle before Ctrl+V.
            await Task.Delay(100, cancellationToken);
            _logger.LogInformation(
                "Smart paste rewrote clipboard. Process={ProcessName} Strategy={Strategy} Length={Length}",
                app.ProcessName,
                strategy,
                pasteText.Length);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Smart paste clipboard prepare failed; will still attempt normal Ctrl+V.");
        }
    }

    private async Task SendNormalPasteAsync(GestureExecutionContext context, CancellationToken cancellationToken)
    {
        TryRestoreGestureTarget(context);
        // Production KeyboardInputSender routes Ctrl+V through hardened injector.
        // This is the ONLY injection path for gesture paste (smart or not).
        _keyboardInputSender.SendShortcut(KeyboardInputNativeMethods.VkControl, KeyboardInputNativeMethods.VkV);
        await RecordPasteAsync(cancellationToken);
    }

    private void TryRestoreGestureTarget(GestureExecutionContext context)
    {
        if (context.TargetWindowHandle == 0)
        {
            return;
        }

        var hwnd = (IntPtr)context.TargetWindowHandle;
        if (!WindowNativeMethods.IsWindow(hwnd))
        {
            return;
        }

        // If target is already foreground, do NOT re-activate — SetForegroundWindow on the
        // top-level window can steal focus from the caret/edit child and make Ctrl+V a no-op.
        var foreground = WindowNativeMethods.GetForegroundWindow();
        if (foreground == hwnd)
        {
            _logger.LogDebug("Gesture paste target already foreground hwnd={Hwnd}", context.TargetWindowHandle);
            return;
        }

        var ok = WindowNativeMethods.TryActivateWindow(hwnd);
        _logger.LogDebug(
            "Gesture paste focus restore hwnd={Hwnd} ok={Ok}",
            context.TargetWindowHandle,
            ok);
    }

    private async Task SearchSelectedTextAsync(string urlFormat, CancellationToken cancellationToken)
    {
        _clipboardService.SuppressCaptureFor(TimeSpan.FromMilliseconds(1200));
        _keyboardInputSender.SendShortcut(KeyboardInputNativeMethods.VkControl, KeyboardInputNativeMethods.VkC);
        await Task.Delay(80, cancellationToken);

        var text = _clipboardTextReader.TryReadText();
        if (string.IsNullOrWhiteSpace(text))
        {
            _logger.LogInformation("Search selected text gesture skipped: clipboard text is empty.");
            return;
        }

        var encoded = Uri.EscapeDataString(text.Trim());
        _urlLauncher.OpenUrl(string.Format(System.Globalization.CultureInfo.InvariantCulture, urlFormat, encoded));
    }

    private async Task ExecuteAssistantActionAsync(BuiltInGestureAction action, CancellationToken cancellationToken)
    {
        var actionId = GestureAssistantActionMap.ToAssistantActionId(action);
        if (actionId is null)
        {
            return;
        }

        var result = await _assistantActionExecutor.ExecuteAsync(
            new AssistantActionRequest(actionId, OutputOverride: AssistantOutputKind.Clipboard),
            cancellationToken);

        _logger.LogInformation(
            "Gesture assistant action finished. GestureAction={GestureAction} AssistantId={AssistantId} Success={Success} ErrorClass={ErrorClass} InputLength={InputLength} OutputLength={OutputLength}",
            action,
            actionId,
            result.Success,
            result.ErrorClass ?? "",
            result.InputLength,
            result.OutputLength);
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
