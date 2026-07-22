using GestureClip.App.Converters;
using GestureClip.App.ViewModels;
using GestureClip.Core.Gestures;
using Xunit;

namespace GestureClip.Tests.App;

public sealed class GestureActionOptionConverterTests
{
    private readonly GestureActionOptionConverter _converter = new();

    [Fact]
    public void Convert_maps_enum_to_catalog_option()
    {
        var option = _converter.Convert(BuiltInGestureAction.SmartPaste, typeof(object), null, null)
            as GestureActionOptionViewModel;

        Assert.NotNull(option);
        Assert.Equal(BuiltInGestureAction.SmartPaste, option!.Action);
    }

    [Fact]
    public void ConvertBack_maps_option_to_enum()
    {
        var source = GestureActionCatalog.DefaultOptions.First(o => o.Action == BuiltInGestureAction.Copy);
        var action = (BuiltInGestureAction)_converter.ConvertBack(source, typeof(BuiltInGestureAction), null, null)!;

        Assert.Equal(BuiltInGestureAction.Copy, action);
    }

    [Fact]
    public void ConvertBack_null_returns_None()
    {
        var action = (BuiltInGestureAction)_converter.ConvertBack(null, typeof(BuiltInGestureAction), null, null)!;
        Assert.Equal(BuiltInGestureAction.None, action);
    }
}
