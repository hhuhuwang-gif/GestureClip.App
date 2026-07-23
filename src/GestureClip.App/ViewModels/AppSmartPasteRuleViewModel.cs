using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using GestureClip.Core.Privacy;

namespace GestureClip.App.ViewModels;

public sealed class AppSmartPasteRuleViewModel : INotifyPropertyChanged
{
    private string _strategy;

    public AppSmartPasteRuleViewModel(
        AppSmartPasteRule rule,
        Func<AppSmartPasteRuleViewModel, Task> updateAsync,
        Func<AppSmartPasteRuleViewModel, Task> deleteAsync)
    {
        ProcessName = rule.ProcessName;
        Note = rule.Note;
        _strategy = rule.Strategy;
        DeleteCommand = new AsyncRelayCommand(_ => deleteAsync(this));
        _updateAsync = updateAsync;
    }

    private readonly Func<AppSmartPasteRuleViewModel, Task> _updateAsync;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string ProcessName { get; }

    public string? Note { get; }

    public string Strategy
    {
        get => _strategy;
        set
        {
            if (_strategy == value)
            {
                return;
            }

            _strategy = value;
            OnPropertyChanged();
            _ = _updateAsync(this);
        }
    }

    public string StrategyDisplay => Strategy switch
    {
        "PlainTextPaste" => "纯文本粘贴",
        "CleanTextPaste" => "干净粘贴",
        "NormalPaste" => "普通粘贴",
        _ => Strategy
    };

    public ICommand DeleteCommand { get; }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
