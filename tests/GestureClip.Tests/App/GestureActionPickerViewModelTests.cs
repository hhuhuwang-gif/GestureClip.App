using GestureClip.App.ViewModels;
using GestureClip.Core.Gestures;
using Xunit;

namespace GestureClip.Tests.App;

public sealed class GestureActionPickerViewModelTests
{
    [Theory]
    [InlineData(" 复制 ", BuiltInGestureAction.Copy)]
    [InlineData("ctrl + c", BuiltInGestureAction.Copy)]
    [InlineData("剪贴板", BuiltInGestureAction.Copy)]
    [InlineData("撤回", BuiltInGestureAction.Undo)]
    public void Search_matches_name_shortcut_category_and_description_without_changing_binding(
        string search, BuiltInGestureAction expected)
    {
        var saves = 0;
        var card = CreateCard(() => saves++);
        var picker = new GestureActionPickerViewModel(card) { SearchText = search };
        Assert.Equal(expected, Assert.Single(picker.FilteredOptions).Action);
        Assert.Equal(BuiltInGestureAction.Paste, card.SelectedAction);
        Assert.Equal(0, saves);
    }

    [Fact]
    public void Clearing_search_restores_all_options_and_reports_empty_results()
    {
        var picker = new GestureActionPickerViewModel(CreateCard(() => { }));
        var notifications = new List<string?>();
        picker.PropertyChanged += (_, e) => notifications.Add(e.PropertyName);
        picker.SearchText = "不存在的动作";
        Assert.Empty(picker.FilteredOptions);
        Assert.Contains("没有匹配", picker.ResultText);
        Assert.Contains(nameof(picker.FilteredOptions), notifications);
        Assert.Contains(nameof(picker.ResultText), notifications);
        picker.SearchText = " ";
        Assert.Equal(2, picker.FilteredOptions.Count);
    }

    private static GestureBindingCardViewModel CreateCard(Action onSave)
    {
        GestureActionOptionViewModel[] options =
        [
            new(BuiltInGestureAction.Copy, "复制", "Ctrl + C", "剪贴板", "复制选中内容"),
            new(BuiltInGestureAction.Undo, "撤销", "Ctrl + Z", "编辑", "撤回上一次操作")
        ];
        return new GestureBindingCardViewModel("U", "向上", "上划", true, BuiltInGestureAction.Paste,
            options, _ => { onSave(); return Task.CompletedTask; }, _ => Task.CompletedTask);
    }
}
