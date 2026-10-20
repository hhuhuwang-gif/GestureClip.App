using GestureClip.Core.Clipboard;

namespace GestureClip.App.ViewModels;

public sealed partial class ClipboardOverlayViewModel
{
    public PasteQueueSnapshot PasteQueueState => _pasteQueueService?.Snapshot ?? new(0, PasteQueueStatus.Empty, null);
    public int PasteQueueCount => PasteQueueState.PendingCount;
    public bool HasPasteQueue => PasteQueueCount > 0;
    public bool CanRecoverPasteQueue => PasteQueueState.Status == PasteQueueStatus.Failed;
    public string PasteQueueText => PasteQueueState.Status switch
    {
        PasteQueueStatus.Failed => $"粘贴暂停，当前项已保留（剩 {PasteQueueCount} 条）：{PasteQueueState.ErrorMessage}",
        PasteQueueStatus.Pasting => $"正在粘贴 · 队列剩 {PasteQueueCount} 条",
        _ => $"粘贴队列剩 {PasteQueueCount} 条 · 在目标应用按 Ctrl+V 依次粘贴"
    };

    public bool EnqueueToPasteQueue(IReadOnlyList<ClipboardItem> items)
    {
        if (_pasteQueueService is null || items.Count == 0) return false;
        if (!_pasteQueueService.Enqueue(items))
        {
            StatusText = "粘贴队列启动失败：Ctrl+V 被其他程序占用";
            return false;
        }
        StatusText = CanRecoverPasteQueue ? PasteQueueText :
            $"已加入粘贴队列（{items.Count} 条）· 到目标应用连按 Ctrl+V 依次粘贴";
        return true;
    }

    public void RetryPasteQueue()
    {
        StatusText = _pasteQueueService?.Retry() == true ?
            "已准备重试 · 回到目标应用按 Ctrl+V" : PasteQueueText;
    }

    public void SkipPasteQueue()
    {
        if (_pasteQueueService?.Skip() == true) StatusText = "已跳过当前项 · 回到目标应用按 Ctrl+V";
    }

    public void ClearPasteQueue()
    {
        _pasteQueueService?.Clear();
        StatusText = "粘贴队列已清空，Ctrl+V 恢复正常";
    }

    private void OnPasteQueueChanged(object? sender, EventArgs args)
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is not null && !dispatcher.CheckAccess())
        {
            dispatcher.BeginInvoke(new Action(NotifyPasteQueueChanged));
            return;
        }
        NotifyPasteQueueChanged();
    }

    private void NotifyPasteQueueChanged()
    {
        OnPropertyChanged(nameof(PasteQueueState));
        OnPropertyChanged(nameof(PasteQueueCount));
        OnPropertyChanged(nameof(HasPasteQueue));
        OnPropertyChanged(nameof(PasteQueueText));
        OnPropertyChanged(nameof(CanRecoverPasteQueue));
    }
}
