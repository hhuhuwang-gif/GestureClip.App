using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using GestureClip.App.ViewModels;
using GestureClip.Core.Abstractions;
using GestureClip.Core.Clipboard;
using GestureClip.Core.Settings;
using GestureClip.App.Services;
using GestureClip.Core.SystemInfo;

namespace GestureClip.App;

public partial class ClipboardOverlayWindow
{
    private readonly IPasteTargetService _pasteTargetService;
    private PasteTarget? _pasteTarget;
    private CancellationTokenSource? _pastePreparation;

    public void CapturePasteTarget()
    {
        _pastePreparation?.Cancel();
        _pasteTarget = _pasteTargetService.CaptureTarget();
    }

    private Task PasteWithFocusRestoreAsync(bool keepOverlayOpen, bool plainText = false) =>
        _pasteCoordinator.PasteAsync(new ClipboardPasteRequest(keepOverlayOpen, plainText));

    private Task PasteByIndexWithFocusRestoreAsync(int index) =>
        _pasteCoordinator.PasteAsync(new ClipboardPasteRequest(false, false, index));

    private async Task PasteCoreAsync(ClipboardPasteRequest request)
    {
        await _viewModel.FlushPendingSearchAsync();
        if (_viewModel.HasSearchError || !HasPasteTarget(request.Index))
        {
            return;
        }
        using var preparation = new CancellationTokenSource();
        _pastePreparation = preparation;
        var restore = request.KeepOverlayOpen;
        try
        {
            var target = _pasteTarget ?? throw new InvalidOperationException("尚未选择粘贴目标。");
            PrepareForExternalPaste();
            await _pasteTargetService.WaitUntilReadyAsync(target, preparation.Token);
            if (request.Index is { } index)
            {
                restore |= !await _viewModel.PasteByIndexAsync(index, target.WindowHandle);
            }
            else
            {
                restore |= !await _viewModel.PasteSelectedAsync(request.KeepOverlayOpen, request.PlainText, target.WindowHandle);
            }
        }
        catch (OperationCanceledException) when (preparation.IsCancellationRequested)
        {
            restore = false;
        }
        catch (Exception exception)
        {
            _viewModel.ReportPastePreparationError(exception.Message);
            restore = true;
        }
        finally
        {
            if (ReferenceEquals(_pastePreparation, preparation)) _pastePreparation = null;
            if (restore)
            {
                RestoreOverlayAfterPaste();
            }
        }
    }

    private bool HasPasteTarget(int? index)
    {
        return index is { } position
            ? position >= 0 && position < _viewModel.Items.Count
            : _viewModel.SelectedItem is not null;
    }

    private void HideOverlayAndReleaseFocus()
    {
        _viewModel.HideShortcutHelp();
        MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
        Hide();
    }

    private void PrepareForExternalPaste()
    {
        _viewModel.HideShortcutHelp();
        Topmost = false;
        MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
        Hide();
    }

    private void RestoreOverlayAfterPaste()
    {
        Show();
        Activate();
        Topmost = _alwaysVisible;
        if (_alwaysVisible)
        {
            Topmost = true;
        }
    }
}
