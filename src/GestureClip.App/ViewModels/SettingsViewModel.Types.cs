using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows.Input;
using GestureClip.Core.Abstractions;
using GestureClip.Core.Diagnostics;
using GestureClip.Core.Settings;
using GestureClip.Core.SystemInfo;
using GestureClip.Core.Gestures;
using GestureClip.Core.Hotkeys;
using GestureClip.Core.Workstation;
using GestureClip.Features.Gestures;
using GestureClip.Features.Workstation;
using GestureClip.App.Services;
using GestureClip.Infrastructure.Paths;
using System.Windows.Data;
using System.Windows.Threading;

namespace GestureClip.App.ViewModels;

public sealed partial class SettingsViewModel
{

    public sealed record GesturePresetOption(GesturePreset Value, string DisplayName)
    {
        public override string ToString() => DisplayName;
    }

    public sealed record RetentionOption(string Label, int Days)
    {
        public override string ToString() => Label;
    }

    public sealed record WorkstationTemplateOption(
        string Name,
        string WorkStartTime,
        string WorkEndTime,
        string LunchStartTime,
        string LunchEndTime,
        string Workdays);

    public sealed record GestureStrokeColorOption(string Name, string Color)
    {
        public string DisplayName => $"{Name}  {Color}";

        public override string ToString() => DisplayName;
    }

    public sealed record GestureTriggerModeViewModel(string Name, string Status, bool IsEnabled);

    public IReadOnlyList<GestureActionOptionViewModel> GestureActionOptions { get; } = GestureActionCatalog.DefaultOptions;

    public ICollectionView GestureActionOptionsView { get; }

    private static readonly string[] GesturePatterns =
    [
        "U", "D", "UD", "DU", "L", "R", "LR", "RL", "DL", "DR",
        "R+L", "UR", "UL", "RU", "RD", "LD", "RDL", "RUD", "URD", "ULD", "RULD"
    ];

    private static readonly HashSet<string> PrimaryGesturePatterns = new(StringComparer.Ordinal)
    {
        "U", "D", "UD", "DU", "L", "R", "LR", "RL", "DL", "DR", "R+L"
    };

    private static IReadOnlyList<RecommendedGestureBindingViewModel> BuildRecommendedGestureBindings()
    {
        return
        [
            new("U", DirectionText("U"), GestureName("U"), BuiltInGestureAction.OpenClipboardOverlay, "向上划，快速打开剪贴板历史。"),
            new("D", DirectionText("D"), GestureName("D"), BuiltInGestureAction.SmartPaste, "向下划，根据当前软件自动选择普通粘贴、纯文本粘贴或干净粘贴。"),
            new("LR", DirectionText("LR"), GestureName("LR"), BuiltInGestureAction.Copy, "先左后右，复制当前选中的文字。")
        ];
    }

    private static string NormalizeGesturePattern(string? pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern))
        {
            return "";
        }

        var normalized = pattern.Trim().ToUpperInvariant();
        if (normalized is "R+L" or "右键+左键" or "右键按住+左键点击")
        {
            return "R+L";
        }

        return new string(normalized.Where(ch => ch is 'U' or 'D' or 'L' or 'R').ToArray());
    }

    private static bool IsValidGesturePattern(string pattern)
    {
        return string.Equals(pattern, "R+L", StringComparison.Ordinal) ||
            pattern.Length is >= 1 and <= 8 && pattern.All(ch => ch is 'U' or 'D' or 'L' or 'R');
    }

    private static string DirectionText(string pattern) => pattern == "R+L"
        ? "右键 + 左键"
        : pattern
        .Replace("U", "↑", StringComparison.Ordinal)
        .Replace("D", "↓", StringComparison.Ordinal)
        .Replace("L", "←", StringComparison.Ordinal)
        .Replace("R", "→", StringComparison.Ordinal);

    private static string GestureName(string pattern) => pattern switch
    {
        "U" => "上划",
        "D" => "下划",
        "UD" => "上下划",
        "DU" => "下上划",
        "L" => "左划",
        "R" => "右划",
        "LR" => "左右划",
        "RL" => "右左划",
        "DL" => "下左划",
        "R+L" => "右键按住 + 左键点击",
        "DR" => "下右划",
        "UR" => "上右划",
        "UL" => "上左划",
        "RU" => "右上划",
        "RD" => "右下划",
        "LD" => "左下划",
        "RDL" => "右下左划",
        "RUD" => "右上下划",
        "URD" => "上右下划",
        "ULD" => "上左下划",
        "RULD" => "右上左下划",
        _ => pattern
    };

    private static string ShortcutText(BuiltInGestureAction action) => action switch
    {
        BuiltInGestureAction.Copy => "Ctrl+C",
        BuiltInGestureAction.Paste => "Ctrl+V",
        BuiltInGestureAction.SmartPaste => "SmartPaste",
        BuiltInGestureAction.Cut => "Ctrl+X",
        BuiltInGestureAction.SelectAll => "Ctrl+A",
        BuiltInGestureAction.Undo => "Ctrl+Z",
        BuiltInGestureAction.Redo => "Ctrl+Y",
        BuiltInGestureAction.Enter => "Enter",
        BuiltInGestureAction.Escape => "Esc",
        BuiltInGestureAction.Delete => "Delete",
        BuiltInGestureAction.Backspace => "Backspace",
        BuiltInGestureAction.SendAltLeft => "Alt+Left",
        BuiltInGestureAction.SendAltRight => "Alt+Right",
        BuiltInGestureAction.OpenClipboardOverlay => "ClipboardOverlay",
        BuiltInGestureAction.PasteLatestClipboardItem => "PasteLatest",
        BuiltInGestureAction.PasteAndEnter => "Ctrl+V Enter",
        BuiltInGestureAction.NewTab => "Ctrl+T",
        BuiltInGestureAction.NextTab => "Ctrl+Tab",
        BuiltInGestureAction.PreviousTab => "Ctrl+Shift+Tab",
        BuiltInGestureAction.ReopenClosedTab => "Ctrl+Shift+T",
        BuiltInGestureAction.Refresh => "F5",
        BuiltInGestureAction.CloseTab => "Ctrl+W",
        BuiltInGestureAction.StartMenu => "Win",
        BuiltInGestureAction.ShowDesktop => "Win+D",
        BuiltInGestureAction.SwitchApp => "Alt+Tab",
        BuiltInGestureAction.TaskSwitcher => "Ctrl+Alt+Tab",
        BuiltInGestureAction.PlayPause => "Media Play/Pause",
        BuiltInGestureAction.VolumeUp => "Volume+",
        BuiltInGestureAction.VolumeDown => "Volume-",
        BuiltInGestureAction.Mute => "Mute",
        BuiltInGestureAction.PreviousTrack => "Previous Track",
        BuiltInGestureAction.NextTrack => "Next Track",
        BuiltInGestureAction.TaskManager => "taskmgr",
        BuiltInGestureAction.SystemSettings => "Win+I",
        BuiltInGestureAction.Sleep => "Sleep",
        BuiltInGestureAction.ZoomIn => "Ctrl+=",
        BuiltInGestureAction.ZoomOut => "Ctrl+-",
        BuiltInGestureAction.ResetZoom => "Ctrl+0",
        BuiltInGestureAction.Home => "Home",
        BuiltInGestureAction.End => "End",
        BuiltInGestureAction.PageUp => "PageUp",
        BuiltInGestureAction.PageDown => "PageDown",
        BuiltInGestureAction.Screenshot => "Win+Shift+S",
        BuiltInGestureAction.NextVirtualDesktop => "Ctrl+Win+Right",
        BuiltInGestureAction.PreviousVirtualDesktop => "Ctrl+Win+Left",
        BuiltInGestureAction.FullScreen => "F11",
        BuiltInGestureAction.PinWindow => "Reserved",
        BuiltInGestureAction.LeftMouseClick => "LeftClick",
        BuiltInGestureAction.LeftMouseDoubleClick => "LeftDoubleClick",
        BuiltInGestureAction.RightMouseClick => "RightClick",
        BuiltInGestureAction.MiddleMouseClick => "MiddleClick",
        BuiltInGestureAction.MouseWheelUp => "WheelUp",
        BuiltInGestureAction.MouseWheelDown => "WheelDown",
        BuiltInGestureAction.SearchSelectedTextWithGoogle => "GoogleSearch",
        BuiltInGestureAction.SearchSelectedTextWithBaidu => "BaiduSearch",
        BuiltInGestureAction.SearchSelectedTextWithBing => "BingSearch",
        BuiltInGestureAction.OpenGoogle => "Google",
        BuiltInGestureAction.OpenBaidu => "Baidu",
        _ => ""
    };

    private sealed record CustomBindingDto(BuiltInGestureAction Action, string Shortcut, bool IsEnabled);
}
