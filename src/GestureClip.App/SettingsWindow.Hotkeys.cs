using System.Windows;
using System.Windows.Input;
using GestureClip.App.ViewModels;
using GestureClip.Core.Hotkeys;
using WpfKey = System.Windows.Input.Key;
using WpfKeyEventArgs = System.Windows.Input.KeyEventArgs;
using WpfModifierKeys = System.Windows.Input.ModifierKeys;
using WpfTextBox = System.Windows.Controls.TextBox;
using WpfButton = System.Windows.Controls.Button;

namespace GestureClip.App;

public partial class SettingsWindow
{
    private readonly Dictionary<WpfTextBox, string> _hotkeyCaptureBaseline = new();
    private WpfTextBox? _activeHotkeyBox;

    private void HotkeyBox_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (sender is not WpfTextBox box)
        {
            return;
        }

        _activeHotkeyBox = box;
        _hotkeyCaptureBaseline[box] = GetBoundHotkeyText(box);
        SetCaptureStatus("正在录制… 请按下 Ctrl/Alt/Shift/Win + 主键（Esc 取消）");
        box.SelectAll();
    }

    private void HotkeyBox_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (sender is not WpfTextBox box)
        {
            return;
        }

        // Keep whatever is bound; don't blank.
        SyncBoxFromViewModel(box);
        if (ReferenceEquals(_activeHotkeyBox, box))
        {
            _activeHotkeyBox = null;
        }

        _hotkeyCaptureBaseline.Remove(box);
    }

    private void HotkeyBox_PreviewKeyDown(object sender, WpfKeyEventArgs e)
    {
        if (sender is not WpfTextBox box)
        {
            return;
        }

        e.Handled = true;
        CaptureHotkeyFromKeyEvent(box, e);
    }

    /// <summary>Record button next to each hotkey field.</summary>
    private void HotkeyRecordButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: string tag })
        {
            return;
        }

        var box = FindHotkeyBoxByTag(tag);
        if (box is null)
        {
            SetCaptureStatus("找不到热键输入框。");
            return;
        }

        box.Focus();
        Keyboard.Focus(box);
        SetCaptureStatus($"正在录制「{DescribeTag(tag)}」… 请按下组合键");
    }

    private void CaptureHotkeyFromKeyEvent(WpfTextBox box, WpfKeyEventArgs e)
    {
        if (e.Key is WpfKey.Escape)
        {
            if (_hotkeyCaptureBaseline.TryGetValue(box, out var baseline))
            {
                ApplyHotkeyText(box, baseline);
            }
            else
            {
                SyncBoxFromViewModel(box);
            }

            SetCaptureStatus("已取消录制。");
            Keyboard.ClearFocus();
            return;
        }

        if (e.Key is WpfKey.Back or WpfKey.Delete)
        {
            ApplyHotkeyText(box, GetDefaultHotkeyFor(box));
            SetCaptureStatus($"已恢复默认：{GetBoundHotkeyText(box)}");
            Keyboard.ClearFocus();
            return;
        }

        var key = e.Key == WpfKey.System ? e.SystemKey : e.Key;
        if (key is WpfKey.LeftCtrl or WpfKey.RightCtrl or WpfKey.LeftAlt or WpfKey.RightAlt
            or WpfKey.LeftShift or WpfKey.RightShift or WpfKey.LWin or WpfKey.RWin
            or WpfKey.None or WpfKey.DeadCharProcessed or WpfKey.ImeProcessed
            or WpfKey.Tab)
        {
            // Wait for a non-modifier key.
            SetCaptureStatus("请继续按主键（需配合 Ctrl/Alt/Shift/Win）…");
            return;
        }

        uint modifiers = 0;
        var mods = Keyboard.Modifiers;
        if (mods.HasFlag(WpfModifierKeys.Control))
        {
            modifiers |= HotkeyModifier.Control;
        }

        if (mods.HasFlag(WpfModifierKeys.Alt))
        {
            modifiers |= HotkeyModifier.Alt;
        }

        if (mods.HasFlag(WpfModifierKeys.Shift))
        {
            modifiers |= HotkeyModifier.Shift;
        }

        if (mods.HasFlag(WpfModifierKeys.Windows))
        {
            modifiers |= HotkeyModifier.Win;
        }

        if (modifiers == 0)
        {
            SetCaptureStatus("无效：必须包含 Ctrl / Alt / Shift / Win 至少一个修饰键。");
            SyncBoxFromViewModel(box);
            return;
        }

        var vk = (uint)KeyInterop.VirtualKeyFromKey(key);
        // Oem3 / tilde: some keyboards report as Oem3 via Key.Oem3
        if (key == WpfKey.Oem3)
        {
            vk = HotkeyVirtualKey.Oem3;
        }

        if (!HotkeyDefinition.TryFromVirtualKey(modifiers, vk, out var hotkey))
        {
            SetCaptureStatus($"无法识别该主键（VK=0x{vk:X2}）。可试字母/数字/F1–F12/空格/方向键。");
            SyncBoxFromViewModel(box);
            return;
        }

        // Round-trip parse to ensure it will save correctly
        if (!HotkeyDefinition.TryParse(hotkey.DisplayText, out var parsed) ||
            parsed.VirtualKey != hotkey.VirtualKey)
        {
            SetCaptureStatus($"组合键解析失败：{hotkey.DisplayText}");
            SyncBoxFromViewModel(box);
            return;
        }

        ApplyHotkeyText(box, parsed.DisplayText);
        _hotkeyCaptureBaseline[box] = parsed.DisplayText;
        SetCaptureStatus($"已设置：{parsed.DisplayText}");
        Keyboard.ClearFocus();
    }

    private WpfTextBox? FindHotkeyBoxByTag(string tag)
    {
        return FindVisualChildren<WpfTextBox>(this)
            .FirstOrDefault(b => string.Equals(b.Tag as string, tag, StringComparison.Ordinal));
    }

    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject parent)
        where T : DependencyObject
    {
        if (parent is null)
        {
            yield break;
        }

        var count = System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent);
        for (var i = 0; i < count; i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
            if (child is T match)
            {
                yield return match;
            }

            foreach (var nested in FindVisualChildren<T>(child))
            {
                yield return nested;
            }
        }
    }

    private static string DescribeTag(string tag) => tag switch
    {
        "OpenClipboard" => "打开历史",
        "OpenQuickAction" => "快捷动作",
        "PastePlainText" => "纯文本粘贴",
        _ => tag
    };

    private static string GetDefaultHotkeyFor(WpfTextBox box) =>
        (box.Tag as string) switch
        {
            "OpenClipboard" => HotkeyDefinition.DefaultOpenClipboardOverlay,
            "OpenQuickAction" => HotkeyDefinition.DefaultOpenQuickActionCenter,
            "PastePlainText" => HotkeyDefinition.DefaultPastePlainText,
            _ => HotkeyDefinition.DefaultOpenClipboardOverlay
        };

    private string GetBoundHotkeyText(WpfTextBox box)
    {
        if (DataContext is not SettingsViewModel vm)
        {
            return box.Text ?? string.Empty;
        }

        return (box.Tag as string) switch
        {
            "OpenClipboard" => vm.OpenClipboardHotkeyText,
            "OpenQuickAction" => vm.OpenQuickActionHotkeyText,
            "PastePlainText" => vm.PastePlainTextHotkeyText,
            _ => box.Text ?? string.Empty
        };
    }

    private void SyncBoxFromViewModel(WpfTextBox box)
    {
        var text = GetBoundHotkeyText(box);
        if (!string.Equals(box.Text, text, StringComparison.Ordinal))
        {
            box.Text = text;
        }
    }

    private void ApplyHotkeyText(WpfTextBox box, string text)
    {
        if (DataContext is not SettingsViewModel vm)
        {
            box.Text = text;
            return;
        }

        switch (box.Tag as string)
        {
            case "OpenClipboard":
                vm.OpenClipboardHotkeyText = text;
                box.Text = vm.OpenClipboardHotkeyText;
                break;
            case "OpenQuickAction":
                vm.OpenQuickActionHotkeyText = text;
                box.Text = vm.OpenQuickActionHotkeyText;
                break;
            case "PastePlainText":
                vm.PastePlainTextHotkeyText = text;
                box.Text = vm.PastePlainTextHotkeyText;
                break;
            default:
                box.Text = text;
                break;
        }
    }

    private void SetCaptureStatus(string message)
    {
        if (DataContext is SettingsViewModel vm)
        {
            vm.HotkeyCaptureStatusText = message;
        }
    }
}
