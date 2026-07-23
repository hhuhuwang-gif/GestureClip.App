using GestureClip.App.ViewModels;
using GestureClip.Core.Gestures;
using Xunit;

namespace GestureClip.Tests.App;

public sealed class GestureOptionToStringTests
{
    [Fact]
    public void GesturePresetOption_ToString_returns_display_name()
    {
        var option = new SettingsViewModel.GesturePresetOption(GesturePreset.EditEnhanced, "编辑增强");
        Assert.Equal("编辑增强", option.ToString());
        Assert.Equal("编辑增强", option.DisplayName);
    }

    [Fact]
    public void GestureStrokeColorOption_ToString_returns_display_name()
    {
        var option = new SettingsViewModel.GestureStrokeColorOption("天蓝", "#8CC8FF");
        Assert.Contains("天蓝", option.ToString());
        Assert.Contains("#8CC8FF", option.ToString());
    }
}
