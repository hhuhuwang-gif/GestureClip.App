using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;

namespace GestureClip.App.ViewModels;

public sealed class GestureOverlayViewModel : INotifyPropertyChanged
{
    private string _directionText = "按住右键拖动";
    private string _pattern = "-";
    private string _actionName = "未绑定";
    private string _shortcutText = "暂无动作";
    private string _presetName = "";
    private PointCollection _points = [];

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Pattern
    {
        get => _pattern;
        set
        {
            if (_pattern == value)
            {
                return;
            }

            _pattern = value;
            OnPropertyChanged();
        }
    }

    public string DirectionText
    {
        get => _directionText;
        set
        {
            if (_directionText == value)
            {
                return;
            }

            _directionText = value;
            OnPropertyChanged();
        }
    }

    public string ActionName
    {
        get => _actionName;
        set
        {
            if (_actionName == value)
            {
                return;
            }

            _actionName = value;
            OnPropertyChanged();
        }
    }

    public string ShortcutText
    {
        get => _shortcutText;
        set
        {
            if (_shortcutText == value)
            {
                return;
            }

            _shortcutText = value;
            OnPropertyChanged();
        }
    }

    public string PresetName
    {
        get => _presetName;
        set
        {
            if (_presetName == value)
            {
                return;
            }

            _presetName = value;
            OnPropertyChanged();
        }
    }

    public PointCollection Points
    {
        get => _points;
        set
        {
            _points = value;
            OnPropertyChanged();
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
