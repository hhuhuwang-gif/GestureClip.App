using GestureClip.App.ViewModels;
using GestureClip.Core.Gestures;
using GestureClip.Features.Gestures;
using Xunit;

namespace GestureClip.Tests.Gestures;

public sealed class GestureActionCatalogTests
{
    [Fact]
    public void Mouse_click_actions_are_not_offered_as_shortcut_actions()
    {
        Assert.DoesNotContain(GestureActionCatalog.DefaultOptions, item => item.Action == BuiltInGestureAction.LeftMouseClick);
        Assert.DoesNotContain(GestureActionCatalog.DefaultOptions, item => item.Action == BuiltInGestureAction.LeftMouseDoubleClick);
        Assert.DoesNotContain(GestureActionCatalog.DefaultOptions, item => item.Action == BuiltInGestureAction.RightMouseClick);
        Assert.DoesNotContain(GestureActionCatalog.DefaultOptions, item => item.Action == BuiltInGestureAction.MiddleMouseClick);
        Assert.DoesNotContain(GestureActionCatalog.DefaultOptions, item => item.Action == BuiltInGestureAction.MouseWheelUp);
        Assert.DoesNotContain(GestureActionCatalog.DefaultOptions, item => item.Action == BuiltInGestureAction.MouseWheelDown);
    }

    [Theory]
    [InlineData(BuiltInGestureAction.SearchSelectedTextWithGoogle, "网页搜索")]
    [InlineData(BuiltInGestureAction.SearchSelectedTextWithBaidu, "网页搜索")]
    [InlineData(BuiltInGestureAction.OpenGoogle, "网页搜索")]
    public void Web_search_actions_are_grouped_as_web_search(BuiltInGestureAction action, string expectedCategory)
    {
        var option = GestureActionCatalog.Option(action);

        Assert.Equal(expectedCategory, option.Category);
    }

    [Fact]
    public void EditEnhanced_maps_down_left_to_paste_and_enter()
    {
        var provider = new GesturePresetProvider();

        Assert.Equal(BuiltInGestureAction.PasteAndEnter, provider.GetAction(GesturePreset.EditEnhanced, "DL"));
    }
}
