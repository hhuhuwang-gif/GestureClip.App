using GestureClip.Core.Gestures;
using GestureClip.Features.Assistant;
using Xunit;

namespace GestureClip.Tests.Assistant;

public sealed class GestureAssistantActionMapTests
{
    [Theory]
    [InlineData(BuiltInGestureAction.AssistantTrim, BuiltInAssistantActionCatalog.TrimId)]
    [InlineData(BuiltInGestureAction.AssistantJsonFormat, BuiltInAssistantActionCatalog.JsonFormatId)]
    [InlineData(BuiltInGestureAction.AssistantUrlEncode, BuiltInAssistantActionCatalog.UrlEncodeId)]
    public void Maps_assistant_gesture_actions(BuiltInGestureAction gestureAction, string expectedId)
    {
        Assert.Equal(expectedId, GestureAssistantActionMap.ToAssistantActionId(gestureAction));
        Assert.True(GestureAssistantActionMap.IsAssistantTextAction(gestureAction));
    }

    [Fact]
    public void Non_assistant_actions_return_null()
    {
        Assert.Null(GestureAssistantActionMap.ToAssistantActionId(BuiltInGestureAction.Copy));
        Assert.False(GestureAssistantActionMap.IsAssistantTextAction(BuiltInGestureAction.OpenQuickActionCenter));
    }
}
