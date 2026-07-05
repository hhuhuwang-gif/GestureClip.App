using System.ComponentModel;
using System.Runtime.CompilerServices;
using GestureClip.Core.Gestures;
using System.Windows.Input;

namespace GestureClip.App.ViewModels;

public sealed class GestureBindingCardViewModel : INotifyPropertyChanged
{
    private readonly Func<GestureBindingCardViewModel, Task> _saveAsync;
    private BuiltInGestureAction _selectedAction;

    public GestureBindingCardViewModel(
        string pattern,
        string directionText,
        string gestureName,
        bool isCommon,
        BuiltInGestureAction selectedAction,
        IReadOnlyList<GestureActionOptionViewModel> actionOptions,
        Func<GestureBindingCardViewModel, Task> saveAsync,
        Func<GestureBindingCardViewModel, Task> deleteAsync)
    {
        Pattern = pattern;
        DirectionText = directionText;
        GestureName = gestureName;
        IsCommon = isCommon;
        _selectedAction = selectedAction;
        ActionOptions = actionOptions;
        _saveAsync = saveAsync;
        DeleteCommand = new AsyncRelayCommand(_ => deleteAsync(this));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Pattern { get; }

    public string DirectionText { get; }

    public string GestureName { get; }

    public bool IsCommon { get; }

    public bool IsBound => SelectedAction != BuiltInGestureAction.None;

    public string PatternText => $"手势码：{Pattern}";

    public string BindingStatusText => IsBound ? "已绑定" : "未绑定";

    public IReadOnlyList<GestureActionOptionViewModel> ActionOptions { get; }

    public ICommand DeleteCommand { get; }

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
            OnPropertyChanged(nameof(IsBound));
            OnPropertyChanged(nameof(BindingStatusText));
            _ = _saveAsync(this);
        }
    }

    public string ActionName => GestureActionText.Name(SelectedAction);

    public string ShortcutText => GestureActionText.Shortcut(SelectedAction);

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
