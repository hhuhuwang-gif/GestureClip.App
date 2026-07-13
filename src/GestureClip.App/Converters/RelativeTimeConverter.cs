using System.Globalization;
using System.Windows.Data;

namespace GestureClip.App.Converters;

public sealed class RelativeTimeConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not DateTimeOffset dto)
        {
            return value?.ToString() ?? "";
        }

        var local = dto.ToLocalTime();
        var delta = DateTimeOffset.Now - local;
        if (delta < TimeSpan.FromSeconds(45))
        {
            return "刚刚";
        }

        if (delta < TimeSpan.FromMinutes(60))
        {
            return $"{Math.Max(1, (int)delta.TotalMinutes)} 分钟前";
        }

        if (delta < TimeSpan.FromHours(24))
        {
            return $"{Math.Max(1, (int)delta.TotalHours)} 小时前";
        }

        if (delta < TimeSpan.FromDays(7))
        {
            return $"{Math.Max(1, (int)delta.TotalDays)} 天前";
        }

        return local.ToString("MM-dd HH:mm", culture);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
