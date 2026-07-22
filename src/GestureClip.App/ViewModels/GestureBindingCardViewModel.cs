using System.ComponentModel;
using System.Runtime.CompilerServices;
using GestureClip.Core.Gestures;
using System.Windows.Input;

namespace GestureClip.App.ViewModels;

public sealed class GestureBindingCardViewModel : INotifyPropertyChanged
{
    private readonly Func<GestureBindingCardViewModel, Task> _saveAsync;
    private readonly Func<string, BuiltInGestureAction> _leftButtonActionResolver;
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
        Func<GestureBindingCardViewModel, Task> deleteAsync,
        Func<string, BuiltInGestureAction>? leftButtonActionResolver = null)
    {
        Pattern = pattern;
        DirectionText = directionText;
        GestureName = gestureName;
        IsCommon = isCommon;
        _selectedAction = selectedAction;
        ActionOptions = actionOptions;
        _saveAsync = saveAsync;
        _leftButtonActionResolver = leftButtonActionResolver ?? (_ => BuiltInGestureAction.None);
        DeleteCommand = new AsyncRelayCommand(_ => deleteAsync(this));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Pattern { get; }

    public string DirectionText { get; }

    public string ShortDirectionText => string.Equals(Pattern, "R+L", StringComparison.Ordinal)
        ? "R+L"
        : DirectionText;

    /// <summary>Mini trajectory glyphs for binding cards (e.g. ↑ → ↓).</summary>
    public string TrajectoryGlyphs
    {
        get
        {
            if (string.Equals(Pattern, "R+L", StringComparison.OrdinalIgnoreCase))
            {
                return "R+L";
            }

            if (string.IsNullOrWhiteSpace(Pattern))
            {
                return "·";
            }

            var map = new System.Text.StringBuilder();
            foreach (var ch in Pattern.ToUpperInvariant())
            {
                map.Append(ch switch
                {
                    'U' => '↑',
                    'D' => '↓',
                    'L' => '←',
                    'R' => '→',
                    _ => ch
                });
            }

            return map.ToString();
        }
    }

    public string GestureName { get; }

    public bool IsCommon { get; }

    public bool IsBound => SelectedAction != BuiltInGestureAction.None;

    public string PatternText => $"手势码：{Pattern}";

    public string BindingStatusText => IsBound ? "已绑定" : "未绑定";

    public string DisplayText => $"{GestureName}  {PatternText}  →  {ActionName}";

    public string ActionSummaryText => $"{PatternText}  →  {ActionName}";

    public string PrimaryActionLabel => "普通动作";

    public string PrimaryActionValueText => ActionName;

    public string LeftButtonModifierLabel => "点左键增强";

    public string LeftButtonModifierValueText => GetLeftButtonModifierValueText();

    public bool HasLeftButtonModifierAction => !string.Equals(LeftButtonModifierValueText, "暂无增强动作", StringComparison.Ordinal);

    public string LeftButtonModifierBadgeText => $"{LeftButtonModifierLabel}：{LeftButtonModifierValueText}";

    public string InstructionText => IsBound
        ? $"按住右键画这个手势后，会执行普通动作。画手势时再点一下左键，会执行增强动作。{LeftButtonModifierHintText}"
        : "按住右键画这个手势后，当前不会执行普通动作。可在下方“左键增强动作”区域配置增强版。";

    public string LeftButtonModifierHintText
    {
        get
        {
            var left = ResolveLeftButtonAction();
            if (left == BuiltInGestureAction.None)
            {
                return "左键增强：暂无。可在上方“左键增强动作”区添加。";
            }

            if (left == BuiltInGestureAction.SmartPaste)
            {
                return "左键增强：智能粘贴（会尽量走干净/纯文本粘贴）。";
            }

            return $"左键增强：{GestureActionText.Name(left)}。";
        }
    }

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

    public GestureActionOptionViewModel? SelectedActionOption
    {
        get => ActionOptions.FirstOrDefault(o => o.Action == SelectedAction);
        set
        {
            if (value is null || value.Action == SelectedAction)
            {
                return;
            }

            SelectedAction = value.Action;
        }
    }

    public string ActionName => GestureActionText.Name(SelectedAction);

    public string ShortcutText => GestureActionText.Shortcut(SelectedAction);

    public void SetSelectedActionWithoutSaving(BuiltInGestureAction action)
    {
        SetSelectedAction(action, save: false);
    }

    public void RefreshLeftButtonModifierDisplay()
    {
        OnPropertyChanged(nameof(LeftButtonModifierValueText));
        OnPropertyChanged(nameof(HasLeftButtonModifierAction));
        OnPropertyChanged(nameof(LeftButtonModifierBadgeText));
        OnPropertyChanged(nameof(InstructionText));
        OnPropertyChanged(nameof(LeftButtonModifierHintText));
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
        OnPropertyChanged(nameof(SelectedActionOption));
        OnPropertyChanged(nameof(ShortcutText));
        OnPropertyChanged(nameof(IsBound));
        OnPropertyChanged(nameof(BindingStatusText));
        OnPropertyChanged(nameof(DisplayText));
        OnPropertyChanged(nameof(ActionSummaryText));
        OnPropertyChanged(nameof(PrimaryActionValueText));
        RefreshLeftButtonModifierDisplay();
        if (save)
        {
            _ = _saveAsync(this);
        }
    }

    private BuiltInGestureAction ResolveLeftButtonAction()
    {
        try
        {
            return _leftButtonActionResolver(Pattern);
        }
        catch
        {
            return BuiltInGestureAction.None;
        }
    }

    private string GetLeftButtonModifierValueText()
    {
        var left = ResolveLeftButtonAction();
        if (left == BuiltInGestureAction.None)
        {
            return "暂无增强动作";
        }

        if (left == BuiltInGestureAction.SmartPaste)
        {
            return "智能粘贴 / 干净粘贴";
        }

        return GestureActionText.Name(left);
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
