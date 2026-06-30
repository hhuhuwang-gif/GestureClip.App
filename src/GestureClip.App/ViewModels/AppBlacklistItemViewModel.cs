using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using GestureClip.Core.Privacy;

namespace GestureClip.App.ViewModels;

public sealed class AppBlacklistItemViewModel : INotifyPropertyChanged
{
    private readonly Func<AppBlacklistItemViewModel, Task> _updateAsync;
    private bool _blockClipboard;
    private bool _blockGesture;

    public AppBlacklistItemViewModel(
        AppBlacklistItem item,
        Func<AppBlacklistItemViewModel, Task> updateAsync,
        Func<AppBlacklistItemViewModel, Task> deleteAsync)
    {
        Id = item.Id;
        ProcessName = item.ProcessName;
        Reason = item.Reason;
        _blockClipboard = item.BlockClipboard;
        _blockGesture = item.BlockGesture;
        _updateAsync = updateAsync;
        DeleteCommand = new AsyncRelayCommand(_ => deleteAsync(this));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public Guid Id { get; }

    public string ProcessName { get; }

    public string? Reason { get; }

    public bool BlockClipboard
    {
        get => _blockClipboard;
        set
        {
            if (_blockClipboard == value)
            {
                return;
            }

            _blockClipboard = value;
            OnPropertyChanged();
            _ = _updateAsync(this);
        }
    }

    public bool BlockGesture
    {
        get => _blockGesture;
        set
        {
            if (_blockGesture == value)
            {
                return;
            }

            _blockGesture = value;
            OnPropertyChanged();
            _ = _updateAsync(this);
        }
    }

    public ICommand DeleteCommand { get; }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
