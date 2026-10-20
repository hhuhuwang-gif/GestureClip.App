namespace GestureClip.App.ViewModels;

public sealed partial class ClipboardOverlayViewModel
{
    public void ReportPastePreparationError(string message)
    {
        ErrorMessage = message;
        StatusText = "未发送粘贴，请重新选择目标应用";
    }
}
