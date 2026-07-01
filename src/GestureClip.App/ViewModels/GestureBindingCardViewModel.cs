using System.ComponentModel;
using System.Runtime.CompilerServices;
using GestureClip.Core.Gestures;

namespace GestureClip.App.ViewModels;

public sealed class GestureBindingCardViewModel : INotifyPropertyChanged
{
    private readonly Func<GestureBindingCardViewModel, Task> _saveAsync;
    private BuiltInGestureAction _selectedAction;

    public GestureBindingCardViewModel(
        string pattern,
        string directionText,
        string gestureName,
        BuiltInGestureAction selectedAction,
        IReadOnlyList<BuiltInGestureAction> actionOptions,
        Func<GestureBindingCardViewModel, Task> saveAsync)
    {
        Pattern = pattern;
        DirectionText = directionText;
        GestureName = gestureName;
        _selectedAction = selectedAction;
        ActionOptions = actionOptions;
        _saveAsync = saveAsync;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Pattern { get; }

    public string DirectionText { get; }

    public string GestureName { get; }

    public IReadOnlyList<BuiltInGestureAction> ActionOptions { get; }

    public BuiltInGestureAction SelectedAction
    {
        get => _selectedAction;
        set
        {
            if (_selectedAction == value)
            {
                return;
            }

            _selectedAction = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ActionName));
            OnPropertyChanged(nameof(ShortcutText));
            _ = _saveAsync(this);
        }
    }

    public string ActionName => SelectedAction.ToString();

    public string ShortcutText => SelectedAction switch
    {
        BuiltInGestureAction.Copy => "Ctrl + C",
        BuiltInGestureAction.Paste => "Ctrl + V",
        BuiltInGestureAction.Cut => "Ctrl + X",
        BuiltInGestureAction.SelectAll => "Ctrl + A",
        BuiltInGestureAction.Undo => "Ctrl + Z",
        BuiltInGestureAction.Redo => "Ctrl + Y",
        BuiltInGestureAction.Enter => "Enter",
        BuiltInGestureAction.Escape => "Esc",
        BuiltInGestureAction.Delete => "Delete",
        BuiltInGestureAction.Backspace => "Backspace",
        BuiltInGestureAction.SendAltLeft => "Alt + ←",
        BuiltInGestureAction.SendAltRight => "Alt + →",
        BuiltInGestureAction.OpenClipboardOverlay => "打开剪贴板历史",
        BuiltInGestureAction.PasteLatestClipboardItem => "粘贴最近一条",
        BuiltInGestureAction.PasteAndEnter => "Ctrl + V, Enter",
        BuiltInGestureAction.NewTab => "Ctrl + T",
        BuiltInGestureAction.ReopenClosedTab => "Ctrl + Shift + T",
        BuiltInGestureAction.Refresh => "F5",
        BuiltInGestureAction.CloseTab => "Ctrl + W",
        BuiltInGestureAction.StartMenu => "Win",
        BuiltInGestureAction.ShowDesktop => "Win + D",
        BuiltInGestureAction.SwitchApp => "Alt + Tab",
        BuiltInGestureAction.TaskSwitcher => "Ctrl + Alt + Tab",
        BuiltInGestureAction.PlayPause => "播放 / 暂停",
        BuiltInGestureAction.VolumeUp => "音量 +",
        BuiltInGestureAction.VolumeDown => "音量 -",
        BuiltInGestureAction.Mute => "静音",
        BuiltInGestureAction.PreviousTrack => "上一曲",
        BuiltInGestureAction.NextTrack => "下一曲",
        BuiltInGestureAction.TaskManager => "任务管理器",
        BuiltInGestureAction.SystemSettings => "Win + I",
        BuiltInGestureAction.Sleep => "休眠",
        BuiltInGestureAction.ZoomIn => "Ctrl + =",
        BuiltInGestureAction.ZoomOut => "Ctrl + -",
        BuiltInGestureAction.ResetZoom => "Ctrl + 0",
        BuiltInGestureAction.Home => "Home",
        BuiltInGestureAction.End => "End",
        BuiltInGestureAction.PageUp => "PageUp",
        BuiltInGestureAction.PageDown => "PageDown",
        BuiltInGestureAction.Screenshot => "Win + Shift + S",
        BuiltInGestureAction.NextVirtualDesktop => "Ctrl + Win + →",
        BuiltInGestureAction.PreviousVirtualDesktop => "Ctrl + Win + ←",
        BuiltInGestureAction.FullScreen => "F11",
        BuiltInGestureAction.PinWindow => "预留",
        _ => "暂无动作"
    };

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
