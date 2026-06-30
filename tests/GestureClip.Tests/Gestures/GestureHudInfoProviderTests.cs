using GestureClip.Core.Gestures;
using GestureClip.Features.Gestures;
using Xunit;

namespace GestureClip.Tests.Gestures;

public sealed class GestureHudInfoProviderTests
{
    [Theory]
    [InlineData(GesturePreset.EditEnhanced, "U", "↑", "复制", "Ctrl + C", "编辑增强模式")]
    [InlineData(GesturePreset.EditEnhanced, "D", "↓", "粘贴", "Ctrl + V", "编辑增强模式")]
    [InlineData(GesturePreset.EditEnhanced, "UD", "↑↓", "确认", "Enter", "编辑增强模式")]
    [InlineData(GesturePreset.EditEnhanced, "DU", "↓↑", "取消", "Esc", "编辑增强模式")]
    [InlineData(GesturePreset.ClipboardEnhanced, "U", "↑", "打开剪贴板历史", "剪贴板面板", "剪贴板增强模式")]
    [InlineData(GesturePreset.ClipboardEnhanced, "D", "↓", "粘贴最近一条", "历史粘贴", "剪贴板增强模式")]
    public void GetInfo_returns_human_readable_hud_text(
        GesturePreset preset,
        string pattern,
        string direction,
        string actionName,
        string shortcutText,
        string presetName)
    {
        var provider = new GestureHudInfoProvider(new GesturePresetProvider());

        var info = provider.GetInfo(preset, pattern);

        Assert.Equal(direction, info.DirectionText);
        Assert.Equal(pattern, info.Pattern);
        Assert.Equal(actionName, info.ActionName);
        Assert.Equal(shortcutText, info.ShortcutText);
        Assert.Equal(presetName, info.PresetName);
    }

    [Fact]
    public void GetInfo_marks_unknown_pattern_as_unbound()
    {
        var provider = new GestureHudInfoProvider(new GesturePresetProvider());

        var info = provider.GetInfo(GesturePreset.EditEnhanced, "LD");

        Assert.Equal("←↓", info.DirectionText);
        Assert.Equal("LD", info.Pattern);
        Assert.Equal("未绑定", info.ActionName);
        Assert.Equal("暂无动作", info.ShortcutText);
    }
}
