using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using GestureClip.Core.Abstractions;
using GestureClip.Core.Settings;
using GestureClip.Core.Workstation;

namespace GestureClip.App.ViewModels;

using Media = System.Windows.Media;

public sealed partial class WorkstationDashboardViewModel : INotifyPropertyChanged
{
    private static string FormatDuration(TimeSpan value)
    {
        if (value <= TimeSpan.Zero) return "0 分钟";
        if (value.TotalHours >= 1) return $"{(int)value.TotalHours}小时 {value.Minutes}分钟";
        return $"{Math.Max(0, value.Minutes)}分钟";
    }

    private static string FormatDurationClock(TimeSpan value)
    {
        if (value <= TimeSpan.Zero) return "到点了";
        return $"{(int)value.TotalHours:D2}:{value.Minutes:D2}:{value.Seconds:D2}";
    }


    private static string FormatMoney(decimal value) => $"￥{value:F2}";
}
