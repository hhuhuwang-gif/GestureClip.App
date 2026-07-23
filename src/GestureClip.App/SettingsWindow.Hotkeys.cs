using System.Windows.Input;
using GestureClip.App.ViewModels;
using GestureClip.Core.Hotkeys;
using WpfKey = System.Windows.Input.Key;
using WpfKeyEventArgs = System.Windows.Input.KeyEventArgs;
using WpfModifierKeys = System.Windows.Input.ModifierKeys;
using WpfTextBox = System.Windows.Controls.TextBox;

namespace GestureClip.App;

public partial class SettingsWindow
{
    private readonly Dictionary<WpfTextBox, string> _hotkeyCaptureBaseline = new();

    private void HotkeyBox_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (sender is not WpfTextBox box)
        {
            return;
        }

        _hotkeyCaptureBaseline[box] = box.Text ?? string.Empty;
        box.Text = "按下新快捷键…";
        box.SelectAll();
    }

    private void HotkeyBox_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (sender is not WpfTextBox box)
        {
            return;
        }

        if (string.Equals(box.Text, "按下新快捷键…", StringComparison.Ordinal) &&
            _hotkeyCaptureBaseline.TryGetValue(box, out var baseline))
        {
            ApplyHotkeyText(box, baseline);
        }
        else if (string.Equals(box.Text, "需含 Ctrl/Alt/Shift + 键", StringComparison.Ordinal) &&
                 _hotkeyCaptureBaseline.TryGetValue(box, out var baseline2))
        {
            ApplyHotkeyText(box, baseline2);
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

        if (e.Key is WpfKey.Escape)
        {
            if (_hotkeyCaptureBaseline.TryGetValue(box, out var baseline))
            {
                ApplyHotkeyText(box, baseline);
            }

            Keyboard.ClearFocus();
            return;
        }

        if (e.Key is WpfKey.Back or WpfKey.Delete)
        {
            ApplyHotkeyText(box, GetDefaultHotkeyFor(box));
            Keyboard.ClearFocus();
            return;
        }

        var key = e.Key == WpfKey.System ? e.SystemKey : e.Key;
        if (key is WpfKey.LeftCtrl or WpfKey.RightCtrl or WpfKey.LeftAlt or WpfKey.RightAlt
            or WpfKey.LeftShift or WpfKey.RightShift or WpfKey.LWin or WpfKey.RWin
            or WpfKey.None or WpfKey.DeadCharProcessed or WpfKey.ImeProcessed)
        {
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

        var vk = (uint)KeyInterop.VirtualKeyFromKey(key);
        if (!HotkeyDefinition.TryFromVirtualKey(modifiers, vk, out var hotkey))
        {
            box.Text = "需含 Ctrl/Alt/Shift + 键";
            return;
        }

        ApplyHotkeyText(box, hotkey.DisplayText);
        _hotkeyCaptureBaseline[box] = hotkey.DisplayText;
        Keyboard.ClearFocus();
    }

    private static string GetDefaultHotkeyFor(WpfTextBox box) =>
        (box.Tag as string) switch
        {
            "OpenClipboard" => HotkeyDefinition.DefaultOpenClipboardOverlay,
            "OpenQuickAction" => HotkeyDefinition.DefaultOpenQuickActionCenter,
            "PastePlainText" => HotkeyDefinition.DefaultPastePlainText,
            _ => HotkeyDefinition.DefaultOpenClipboardOverlay
        };

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
}
