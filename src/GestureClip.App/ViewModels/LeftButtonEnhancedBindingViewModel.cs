using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using GestureClip.Core.Gestures;

namespace GestureClip.App.ViewModels;

public sealed class LeftButtonEnhancedBindingViewModel : INotifyPropertyChanged
{
    private string _pattern;
    private BuiltInGestureAction _action;
    private readonly Func<Task> _saveAsync;

    public LeftButtonEnhancedBindingViewModel(
        string pattern,
        BuiltInGestureAction action,
        IReadOnlyList<GestureActionOptionViewModel> actionOptions,
        Func<Task> saveAsync)
    {
        _pattern = pattern;
        _action = action;
        ActionOptions = actionOptions;
        _saveAsync = saveAsync;
        DeleteCommand = new AsyncRelayCommand(_ => DeleteAsync());
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler? DeleteRequested;

    public IReadOnlyList<GestureActionOptionViewModel> ActionOptions { get; }

    public ICommand DeleteCommand { get; }

    public string Pattern
    {
        get => _pattern;
        set
        {
            var normalized = (value ?? "").Trim().ToUpperInvariant();
            if (_pattern == normalized)
            {
                return;
            }

            _pattern = normalized;
            OnPropertyChanged();
            OnPropertyChanged(nameof(DirectionText));
            _ = _saveAsync();
        }
    }

    public BuiltInGestureAction Action
    {
        get => _action;
        set
        {
            if (_action == value)
            {
                return;
            }

            _action = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ActionName));
            _ = _saveAsync();
        }
    }

    public string ActionName => GestureActionText.Name(Action);

    public string DirectionText => Pattern switch
    {
        "U" => "上划",
        "D" => "下划",
        "L" => "左划",
        "R" => "右划",
        "UD" => "上下",
        "DU" => "下上",
        "LR" => "左右",
        "RL" => "右左",
        "DL" => "下左",
        "DR" => "下右",
        "UL" => "上左",
        "UR" => "上右",
        "R+L" => "右键+左键",
        _ => string.IsNullOrWhiteSpace(Pattern) ? "未设置手势" : Pattern
    };

    public string SummaryText =>
        $"手势 {DirectionText}（{Pattern}）+ 点左键 → {ActionName}";

    private Task DeleteAsync()
    {
        DeleteRequested?.Invoke(this, EventArgs.Empty);
        return _saveAsync();
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
