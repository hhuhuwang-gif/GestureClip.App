using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;

namespace GestureClip.App.ViewModels;

public sealed class GestureOverlayViewModel : INotifyPropertyChanged
{
    private string _directionText = "右键";
    private string _pattern = "-";
    private string _actionName = "未绑定";
    private string _shortcutText = "暂无动作";
    private string _presetName = "";
    private string _workStatusText = "低功耗运行期";
    private string _offWorkCountdownText = "--";
    private string _paydayCountdownText = "--";
    private string _todayEarnedText = "￥0.00";
    private string _todayFishingValueText = "￥0.00";
    private string _efficiencyStatsText = "复制 0  ·  粘贴 0  ·  手势 0";
    private string _savedClicksText = "少点了 0 次";
    private string _funText = "右键一滑，效率开挂";
    private string _gainedXpText = "";
    private string _levelText = "Lv.1 初入工位";
    private string _xpText = "XP 0 / 50";
    private double _xpProgressPercent;
    private string _workSummaryText = "今日 ￥0.00 · 下班 -- · 发薪 --";
    private string _statsText = "手势 0  ·  复制 0  ·  粘贴 0  ·  少点 0";
    private PointCollection _points = [];
    private System.Windows.Media.Brush _strokeBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(140, 200, 255));
    private System.Windows.Media.Brush _hudBackgroundBrush = new LinearGradientBrush(
        System.Windows.Media.Color.FromRgb(17, 23, 36),
        System.Windows.Media.Color.FromRgb(30, 41, 59),
        0);
    private System.Windows.Media.Brush _hudAccentBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(147, 197, 253));

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

    public string WorkStatusText
    {
        get => _workStatusText;
        set
        {
            if (_workStatusText == value)
            {
                return;
            }

            _workStatusText = value;
            OnPropertyChanged();
        }
    }

    public string OffWorkCountdownText
    {
        get => _offWorkCountdownText;
        set
        {
            if (_offWorkCountdownText == value)
            {
                return;
            }

            _offWorkCountdownText = value;
            OnPropertyChanged();
        }
    }

    public string PaydayCountdownText
    {
        get => _paydayCountdownText;
        set
        {
            if (_paydayCountdownText == value)
            {
                return;
            }

            _paydayCountdownText = value;
            OnPropertyChanged();
        }
    }

    public string TodayEarnedText
    {
        get => _todayEarnedText;
        set
        {
            if (_todayEarnedText == value)
            {
                return;
            }

            _todayEarnedText = value;
            OnPropertyChanged();
        }
    }

    public string TodayFishingValueText
    {
        get => _todayFishingValueText;
        set
        {
            if (_todayFishingValueText == value)
            {
                return;
            }

            _todayFishingValueText = value;
            OnPropertyChanged();
        }
    }

    public string EfficiencyStatsText
    {
        get => _efficiencyStatsText;
        set
        {
            if (_efficiencyStatsText == value)
            {
                return;
            }

            _efficiencyStatsText = value;
            OnPropertyChanged();
        }
    }

    public string SavedClicksText
    {
        get => _savedClicksText;
        set
        {
            if (_savedClicksText == value)
            {
                return;
            }

            _savedClicksText = value;
            OnPropertyChanged();
        }
    }

    public string FunText
    {
        get => _funText;
        set
        {
            if (_funText == value)
            {
                return;
            }

            _funText = value;
            OnPropertyChanged();
        }
    }

    public string GainedXpText
    {
        get => _gainedXpText;
        set
        {
            if (_gainedXpText == value)
            {
                return;
            }

            _gainedXpText = value;
            OnPropertyChanged();
        }
    }

    public string LevelText
    {
        get => _levelText;
        set
        {
            if (_levelText == value)
            {
                return;
            }

            _levelText = value;
            OnPropertyChanged();
        }
    }

    public string XpText
    {
        get => _xpText;
        set
        {
            if (_xpText == value)
            {
                return;
            }

            _xpText = value;
            OnPropertyChanged();
        }
    }

    public double XpProgressPercent
    {
        get => _xpProgressPercent;
        set
        {
            if (Math.Abs(_xpProgressPercent - value) < 0.0001)
            {
                return;
            }

            _xpProgressPercent = value;
            OnPropertyChanged();
        }
    }

    public string WorkSummaryText
    {
        get => _workSummaryText;
        set
        {
            if (_workSummaryText == value)
            {
                return;
            }

            _workSummaryText = value;
            OnPropertyChanged();
        }
    }

    public string StatsText
    {
        get => _statsText;
        set
        {
            if (_statsText == value)
            {
                return;
            }

            _statsText = value;
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

    public System.Windows.Media.Brush StrokeBrush
    {
        get => _strokeBrush;
        set
        {
            if (ReferenceEquals(_strokeBrush, value))
            {
                return;
            }

            _strokeBrush = value;
            OnPropertyChanged();
        }
    }

    public System.Windows.Media.Brush HudBackgroundBrush
    {
        get => _hudBackgroundBrush;
        set
        {
            if (ReferenceEquals(_hudBackgroundBrush, value))
            {
                return;
            }

            _hudBackgroundBrush = value;
            OnPropertyChanged();
        }
    }

    public System.Windows.Media.Brush HudAccentBrush
    {
        get => _hudAccentBrush;
        set
        {
            if (ReferenceEquals(_hudAccentBrush, value))
            {
                return;
            }

            _hudAccentBrush = value;
            OnPropertyChanged();
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}


