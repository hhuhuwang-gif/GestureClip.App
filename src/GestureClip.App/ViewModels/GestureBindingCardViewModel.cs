using System.ComponentModel;
using System.Runtime.CompilerServices;
using GestureClip.Core.Gestures;
using System.Windows.Input;

namespace GestureClip.App.ViewModels;

public sealed class GestureBindingCardViewModel : INotifyPropertyChanged
{
    private readonly Func<GestureBindingCardViewModel, Task> _saveAsync;
    private BuiltInGestureAction _selectedAction;
    private bool _isSelected;

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

    public string ShortDirectionText => string.Equals(Pattern, "R+L", StringComparison.Ordinal)
        ? "R+L"
        : DirectionText;

    public string GestureName { get; }

    public bool IsCommon { get; }

    public bool IsBound => SelectedAction != BuiltInGestureAction.None;

    public string PatternText => $"手势码：{Pattern}";

    public string BindingStatusText => IsBound ? "已绑定" : "未绑定";

    public string DisplayText => $"{GestureName}  {PatternText}  →  {ActionName}";

    public string ActionSummaryText => $"{PatternText}  →  {ActionName}";

    public string InstructionText => IsBound
        ? "按住右键画这个手势后，会执行这个动作。"
        : "按住右键画这个手势后，当前不会执行动作。";

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value)
            {
                return;
            }

            _isSelected = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(SelectedBadgeText));
        }
    }

    public string SelectedBadgeText => IsSelected ? "当前选中" : "";

    public IReadOnlyList<GestureActionOptionViewModel> ActionOptions { get; }

    public ICommand DeleteCommand { get; }

    public BuiltInGestureAction SelectedAction
    {
        get => _selectedAction;
        set => SetSelectedAction(value, save: true);
    }

    public string ActionName => GestureActionText.Name(SelectedAction);

    public string ShortcutText => GestureActionText.Shortcut(SelectedAction);

    public void SetSelectedActionWithoutSaving(BuiltInGestureAction action)
    {
        SetSelectedAction(action, save: false);
    }

    private void SetSelectedAction(BuiltInGestureAction action, bool save)
    {
        if (_selectedAction == action)
        {
            return;
        }

        _selectedAction = action;
        OnPropertyChanged(nameof(SelectedAction));
        OnPropertyChanged(nameof(ActionName));
        OnPropertyChanged(nameof(ShortcutText));
        OnPropertyChanged(nameof(IsBound));
        OnPropertyChanged(nameof(BindingStatusText));
        OnPropertyChanged(nameof(DisplayText));
        OnPropertyChanged(nameof(ActionSummaryText));
        OnPropertyChanged(nameof(InstructionText));
        if (save)
        {
            _ = _saveAsync(this);
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
