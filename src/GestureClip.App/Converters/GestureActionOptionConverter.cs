using System.Globalization;
using System.Windows.Data;
using GestureClip.App.ViewModels;
using GestureClip.Core.Gestures;

namespace GestureClip.App.Converters;

/// <summary>
/// Maps <see cref="BuiltInGestureAction"/> ↔ <see cref="GestureActionOptionViewModel"/> for ComboBox SelectedItem binding.
/// </summary>
public sealed class GestureActionOptionConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not BuiltInGestureAction action)
        {
            return null;
        }

        return GestureActionCatalog.DefaultOptions.FirstOrDefault(o => o.Action == action);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is GestureActionOptionViewModel option)
        {
            return option.Action;
        }

        return BuiltInGestureAction.None;
    }
}
