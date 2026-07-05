using GestureClip.Core.Gestures;
using GestureClip.App.ViewModels;
using Xunit;

namespace GestureClip.Tests.Gestures;

public sealed class GestureWebSearchActionTests
{
    [Theory]
    [InlineData(BuiltInGestureAction.SearchSelectedTextWithGoogle, "Google 搜索选中文字")]
    [InlineData(BuiltInGestureAction.SearchSelectedTextWithBaidu, "百度搜索选中文字")]
    [InlineData(BuiltInGestureAction.SearchSelectedTextWithBing, "Bing 搜索选中文字")]
    [InlineData(BuiltInGestureAction.OpenGoogle, "打开 Google")]
    [InlineData(BuiltInGestureAction.OpenBaidu, "打开百度")]
    public void Web_search_actions_are_available_in_action_catalog(BuiltInGestureAction action, string expectedName)
    {
        var option = GestureActionCatalog.Option(action);

        Assert.Equal(expectedName, option.Name);
        Assert.Equal("网页搜索", option.Category);
        Assert.Contains(GestureActionCatalog.DefaultOptions, item => item.Action == action);
    }
}
