using GestureClip.Core.Abstractions;
using GestureClip.Core.Gestures;

namespace GestureClip.Features.Gestures;

public sealed class GestureHudInfoProvider : IGestureHudInfoProvider
{
    private readonly IGesturePresetProvider _presetProvider;

    public GestureHudInfoProvider(IGesturePresetProvider presetProvider)
    {
        _presetProvider = presetProvider;
    }

    public GestureHudInfo GetInfo(GesturePreset preset, string? pattern)
    {
        var normalizedPattern = string.IsNullOrWhiteSpace(pattern) ? "-" : pattern;
        var action = normalizedPattern == "-"
            ? BuiltInGestureAction.None
            : _presetProvider.GetAction(preset, normalizedPattern);

        return new GestureHudInfo(
            DirectionText(normalizedPattern),
            normalizedPattern,
            ActionName(action),
            ShortcutText(action),
            PresetName(preset)) { Action = action };
    }

    private static string DirectionText(string pattern)
    {
        if (pattern == "-")
        {
            return "右键";
        }

        return pattern
            .Replace("U", "↑", StringComparison.Ordinal)
            .Replace("D", "↓", StringComparison.Ordinal)
            .Replace("L", "←", StringComparison.Ordinal)
            .Replace("R", "→", StringComparison.Ordinal);
    }

    private static string PresetName(GesturePreset preset)
    {
        return preset switch
        {
            GesturePreset.ClipboardEnhanced => "剪贴板增强模式",
            GesturePreset.Custom => "自定义模式",
            _ => "编辑增强模式"
        };
    }

    private static string ActionName(BuiltInGestureAction action)
    {
        return action switch
        {
            BuiltInGestureAction.Copy => "复制",
            BuiltInGestureAction.Paste => "粘贴",
            BuiltInGestureAction.Cut => "剪切",
            BuiltInGestureAction.SelectAll => "全选",
            BuiltInGestureAction.Undo => "撤销",
            BuiltInGestureAction.Redo => "重做",
            BuiltInGestureAction.Enter => "确认",
            BuiltInGestureAction.Escape => "取消",
            BuiltInGestureAction.Delete => "删除",
            BuiltInGestureAction.Backspace => "退格",
            BuiltInGestureAction.OpenClipboardOverlay => "打开剪贴板历史",
            BuiltInGestureAction.PasteLatestClipboardItem => "粘贴最近一条",
            BuiltInGestureAction.SendAltLeft => "后退",
            BuiltInGestureAction.SendAltRight => "前进",
            BuiltInGestureAction.MinimizeForegroundWindow => "最小化窗口",
            BuiltInGestureAction.CloseForegroundWindow => "关闭窗口",
            _ => "未绑定"
        };
    }

    private static string ShortcutText(BuiltInGestureAction action)
    {
        return action switch
        {
            BuiltInGestureAction.Copy => "Ctrl + C",
            BuiltInGestureAction.Paste => "Ctrl + V",
            BuiltInGestureAction.Cut => "Ctrl + X",
            BuiltInGestureAction.SelectAll => "Ctrl + A",
            BuiltInGestureAction.Undo => "Ctrl + Z",
            BuiltInGestureAction.Redo => "Ctrl + Y",
            BuiltInGestureAction.Enter => "Enter",
            BuiltInGestureAction.Escape => "Esc",
            BuiltInGestureAction.Delete => "Delete",
            BuiltInGestureAction.Backspace => "Backspace",
            BuiltInGestureAction.OpenClipboardOverlay => "剪贴板面板",
            BuiltInGestureAction.PasteLatestClipboardItem => "历史粘贴",
            BuiltInGestureAction.SendAltLeft => "Alt + ←",
            BuiltInGestureAction.SendAltRight => "Alt + →",
            BuiltInGestureAction.MinimizeForegroundWindow => "Win32 Minimize",
            BuiltInGestureAction.CloseForegroundWindow => "WM_CLOSE",
            _ => "暂无动作"
        };
    }
}

