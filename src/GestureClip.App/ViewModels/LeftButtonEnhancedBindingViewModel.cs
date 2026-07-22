using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using GestureClip.Core.Gestures;

namespace GestureClip.App.ViewModels;

public sealed class LeftButtonEnhancedBindingViewModel : INotifyPropertyChanged
{
    public static IReadOnlyList<string> PatternOptions { get; } =
    [
        "U", "D", "L", "R",
        "UD", "DU", "LR", "RL",
        "UL", "UR", "DL", "DR",
        "R+L"
    ];

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

    /// <summary>Instance alias so WPF binding can resolve the static pattern list.</summary>
    public IReadOnlyList<string> AvailablePatterns => PatternOptions;

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
            OnPropertyChanged(nameof(SummaryText));
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
            OnPropertyChanged(nameof(SummaryText));
            OnPropertyChanged(nameof(SelectedActionOption));
            _ = _saveAsync();
        }
    }

    /// <summary>
    /// SelectedItem binding for ComboBox (more reliable than SelectedValue + enum under custom templates).
    /// </summary>
    public GestureActionOptionViewModel? SelectedActionOption
    {
        get => ActionOptions.FirstOrDefault(o => o.Action == _action);
        set
        {
            if (value is null || value.Action == _action)
            {
                return;
            }

            Action = value.Action;
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
        _ => string.IsNullOrWhiteSpace(Pattern) ? "未识别方向" : Pattern
    };

    public string SummaryText =>
        $"手势 {DirectionText}（{Pattern}）+ 左键 → {ActionName}";

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
