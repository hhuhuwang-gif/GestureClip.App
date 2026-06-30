using System.Globalization;
using System.Windows.Data;

namespace GestureClip.App.Converters;

public sealed class ShortcutNumberConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is int index ? (index + 1).ToString(CultureInfo.InvariantCulture) : "";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
