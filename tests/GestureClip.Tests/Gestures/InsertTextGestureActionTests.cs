using GestureClip.Core.Gestures;
using GestureClip.Core.Settings;
using GestureClip.Features.Gestures;
using GestureClip.Tests.TestDoubles;
using Xunit;

namespace GestureClip.Tests.Gestures;

public sealed class InsertTextGestureActionTests
{
    [Fact]
    public void Catalog_lists_date_time_and_snippet_actions()
    {
        var names = GestureClip.App.ViewModels.GestureActionCatalog.DefaultOptions
            .Select(o => o.Action)
            .ToHashSet();

        Assert.Contains(BuiltInGestureAction.InsertDate, names);
        Assert.Contains(BuiltInGestureAction.InsertTime, names);
        Assert.Contains(BuiltInGestureAction.InsertDateTime, names);
        Assert.Contains(BuiltInGestureAction.InsertSnippet1, names);
        Assert.Contains(BuiltInGestureAction.InsertSnippet2, names);
        Assert.Contains(BuiltInGestureAction.InsertSnippet3, names);
    }

    [Fact]
    public void Action_names_are_chinese()
    {
        Assert.Equal("插入今天日期", GestureClip.App.ViewModels.GestureActionText.Name(BuiltInGestureAction.InsertDate));
        Assert.Equal("插入当前时间", GestureClip.App.ViewModels.GestureActionText.Name(BuiltInGestureAction.InsertTime));
        Assert.Equal("插入话术 1", GestureClip.App.ViewModels.GestureActionText.Name(BuiltInGestureAction.InsertSnippet1));
    }

    [Fact]
    public void Setting_keys_exist_for_snippets()
    {
        Assert.Equal("Gesture.Snippet1.Text", SettingKeys.GestureSnippet1Text);
        Assert.Equal("Gesture.Snippet2.Text", SettingKeys.GestureSnippet2Text);
        Assert.Equal("Gesture.Snippet3.Text", SettingKeys.GestureSnippet3Text);
    }
}
