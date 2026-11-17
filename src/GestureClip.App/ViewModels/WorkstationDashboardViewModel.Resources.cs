using System.Windows.Media;
using GestureClip.Core.Workstation;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;

namespace GestureClip.App.ViewModels;

public sealed partial class WorkstationDashboardViewModel
{
    private const byte SoftBrushAlpha = 0x33;
    private readonly FrozenBrushSlot _fishingBrush = new();
    private readonly FrozenBrushSlot _stageAccentBrush = new();
    private readonly FrozenBrushSlot _stageStartBrush = new();
    private readonly FrozenBrushSlot _stageSoftBrush = new();
    private readonly FrozenBrushSlot _restRiskBrush = new();
    private readonly FrozenBrushSlot _restRiskSoftBrush = new();
    private DateOnly? _holidayDate;
    private IReadOnlyList<string> _holidayCountdowns = [];

    private IReadOnlyList<string> GetHolidayCountdowns(DateOnly today)
    {
        if (_holidayDate == today) return _holidayCountdowns;
        var weekend = HolidayCalendar.GetWeekendCountdown(today);
        var items = new List<string>
        {
            weekend.DaysAway == 0 ? "周末进行中" : $"距周末 {weekend.DaysAway} 天"
        };
        foreach (var holiday in HolidayCalendar.GetUpcomingHolidays(today))
        {
            items.Add(holiday.DaysAway == 0 ? $"{holiday.Name}就是今天" : $"距{holiday.Name} {holiday.DaysAway} 天");
        }
        _holidayCountdowns = items.AsReadOnly();
        _holidayDate = today;
        return _holidayCountdowns;
    }

    private sealed class FrozenBrushSlot
    {
        private string? _source;
        private byte? _alpha;
        private SolidColorBrush? _brush;

        public SolidColorBrush Get(string source, byte? alpha = null)
        {
            if (_brush is not null && _source == source && _alpha == alpha) return _brush;
            var color = (Color)ColorConverter.ConvertFromString(source)!;
            if (alpha is { } opacity) color.A = opacity;
            _source = source;
            _alpha = alpha;
            if (_brush?.Color == color) return _brush;
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            _brush = brush;
            return brush;
        }
    }
}
