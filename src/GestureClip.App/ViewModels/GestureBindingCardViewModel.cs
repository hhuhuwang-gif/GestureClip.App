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
        _ => "暂无动作"
    };

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
