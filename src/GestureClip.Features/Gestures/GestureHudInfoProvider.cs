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

    public GestureHudInfo GetInfo(GesturePreset preset, GestureExecutionContext context)
    {
        var action = _presetProvider.GetAction(preset, context);
        var directionText = DirectionText(context.Pattern);
        if (context.IsLeftButtonModified)
        {
            directionText += " + 左键";
        }

        return new GestureHudInfo(
            directionText,
            context.Pattern,
            ActionName(action, context),
            ShortcutText(action, context),
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
            BuiltInGestureAction.SmartPaste => "智能粘贴",
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
            BuiltInGestureAction.PasteAndEnter => "粘贴并回车",
            BuiltInGestureAction.LeftMouseClick => "左键单击",
            BuiltInGestureAction.LeftMouseDoubleClick => "左键双击",
            BuiltInGestureAction.RightMouseClick => "右键单击",
            BuiltInGestureAction.MiddleMouseClick => "中键单击",
            BuiltInGestureAction.MouseWheelUp => "滚轮上",
            BuiltInGestureAction.MouseWheelDown => "滚轮下",
            BuiltInGestureAction.SearchSelectedTextWithGoogle => "Google 搜索",
            BuiltInGestureAction.SearchSelectedTextWithBaidu => "百度搜索",
            BuiltInGestureAction.SearchSelectedTextWithBing => "Bing 搜索",
            BuiltInGestureAction.OpenGoogle => "打开 Google",
            BuiltInGestureAction.OpenBaidu => "打开百度",
            _ => "未绑定"
        };
    }

    private static string ActionName(BuiltInGestureAction action, GestureExecutionContext context)
    {
        if (context.IsLeftButtonModified && action == BuiltInGestureAction.SmartPaste)
        {
            return "干净粘贴";
        }

        return ActionName(action);
    }

    private static string ShortcutText(BuiltInGestureAction action)
    {
        return action switch
        {
            BuiltInGestureAction.Copy => "Ctrl + C",
            BuiltInGestureAction.Paste => "Ctrl + V",
            BuiltInGestureAction.SmartPaste => "根据当前软件自动选择",
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
            BuiltInGestureAction.PasteAndEnter => "Ctrl + V, Enter",
            BuiltInGestureAction.LeftMouseClick => "Mouse Left",
            BuiltInGestureAction.LeftMouseDoubleClick => "Mouse Left ×2",
            BuiltInGestureAction.RightMouseClick => "Mouse Right",
            BuiltInGestureAction.MiddleMouseClick => "Mouse Middle",
            BuiltInGestureAction.MouseWheelUp => "Wheel ↑",
            BuiltInGestureAction.MouseWheelDown => "Wheel ↓",
            BuiltInGestureAction.SearchSelectedTextWithGoogle => "复制选中并搜索",
            BuiltInGestureAction.SearchSelectedTextWithBaidu => "复制选中并搜索",
            BuiltInGestureAction.SearchSelectedTextWithBing => "复制选中并搜索",
            BuiltInGestureAction.OpenGoogle => "google.com",
            BuiltInGestureAction.OpenBaidu => "baidu.com",
            _ => "暂无动作"
        };
    }

    private static string ShortcutText(BuiltInGestureAction action, GestureExecutionContext context)
    {
        if (context.IsLeftButtonModified && action == BuiltInGestureAction.SmartPaste)
        {
            return "强制纯文本 / 干净粘贴";
        }

        return ShortcutText(action);
    }
}
