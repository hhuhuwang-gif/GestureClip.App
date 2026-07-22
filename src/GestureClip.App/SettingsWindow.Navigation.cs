using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;
using GestureClip.App.Controls;
using GestureClip.App.Services;
using GestureClip.App.ViewModels;
using GestureClip.Core.Gestures;
using WpfButton = System.Windows.Controls.Button;
using WpfButtonBase = System.Windows.Controls.Primitives.ButtonBase;
using WpfHorizontalAlignment = System.Windows.HorizontalAlignment;
using WpfKey = System.Windows.Input.Key;
using WpfKeyEventArgs = System.Windows.Input.KeyEventArgs;
using WpfRadioButton = System.Windows.Controls.RadioButton;
using WpfScrollBar = System.Windows.Controls.Primitives.ScrollBar;
using WpfSelector = System.Windows.Controls.Primitives.Selector;
using WpfTextBoxBase = System.Windows.Controls.Primitives.TextBoxBase;
using WpfThickness = System.Windows.Thickness;
using WpfVisibility = System.Windows.Visibility;
using WpfFontWeights = System.Windows.FontWeights;
using WpfBrush = System.Windows.Media.Brush;

namespace GestureClip.App;

public partial class SettingsWindow
{
    private void NavigateToBindings_Click(object sender, RoutedEventArgs e) => NavigateToPage("bindings");

    private void NavigateToEdgeEnhancement_Click(object sender, RoutedEventArgs e)
    {
        NavigateToPage("gestures", "SectionEdgeEnhancement");
        Dispatcher.BeginInvoke(new Action(() =>
        {
            GestureAdvancedSettingsExpander.IsExpanded = true;
            ScrollToNamedElement("EdgeTriggerSettingsGroup");
        }), System.Windows.Threading.DispatcherPriority.Loaded);
    }

    private void ScrollToNamedElement(string elementName)
    {
        var target = FindName(elementName) as FrameworkElement
            ?? FindDescendantByName(this, elementName);

        if (target is null)
        {
            return;
        }

        // Expand parent Expander if any
        var parent = (DependencyObject)target;
        while (parent is not null)
        {
            if (parent is Expander expander)
            {
                expander.IsExpanded = true;
            }

            parent = VisualTreeHelper.GetParent(parent);
        }

        try
        {
            target.BringIntoView();
        }
        catch (InvalidOperationException)
        {
            // ignore if not in visual tree yet
        }
    }

    private static FrameworkElement? FindDescendantByName(DependencyObject root, string name)
    {
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is FrameworkElement fe && string.Equals(fe.Name, name, StringComparison.Ordinal))
            {
                return fe;
            }

            var nested = FindDescendantByName(child, name);
            if (nested is not null)
            {
                return nested;
            }
        }

        return null;
    }

    private static string NormalizePageKey(string? page)
    {
        var key = (page ?? "").Trim().ToLowerInvariant();
        return key switch
        {
            "home" or "首页" or "" => "home",
            "clipboard" or "剪贴板" or "smartpaste" or "智能粘贴" => "clipboard",
            "gestures" or "手势" or "edge" or "边缘" => "gestures",
            "bindings" or "动作" or "动作绑定" or "绑定" or "designer" => "bindings",
            "privacy" or "隐私" or "数据" => "privacy",
            "startup" or "自启" => "startup",
            "workbear" or "小熊" or "工位" => "workbear",
            "diagnostics" or "诊断" => "diagnostics",
            "about" or "关于" => "about",
            _ => "home"
        };
    }

    private static string PageHeaderFromKey(string key) => key switch
    {
        "home" => "首页",
        "clipboard" => "剪贴板",
        "gestures" => "手势",
        "bindings" => "动作绑定",
        "privacy" => "隐私",
        "startup" => "自启",
        "workbear" => "小熊",
        "diagnostics" => "诊断",
        "about" => "关于",
        _ => "首页"
    };

    private void SyncNavSelection(string key)
    {
        _suppressNavSync = true;
        try
        {
            WpfRadioButton? target = key switch
            {
                "home" => NavHome,
                "clipboard" => NavClipboard,
                "gestures" => NavGestures,
                "bindings" => NavBindings,
                "privacy" => NavPrivacy,
                "startup" => NavStartup,
                "workbear" => NavWorkbear,
                "diagnostics" => NavDiagnostics,
                "about" => NavAbout,
                _ => NavHome
            };

            if (target is null)
            {
                return;
            }

            if (target.IsChecked != true)
            {
                target.IsChecked = true;
            }
        }
        finally
        {
            _suppressNavSync = false;
        }
    }

    private void MainSettingsTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressNavSync)
        {
            return;
        }

        if (MainSettingsTabControl?.SelectedItem is not TabItem tab)
        {
            return;
        }

        var header = tab.Header?.ToString() ?? "首页";
        var key = NormalizePageKey(header);
        SyncNavSelection(key);
    }

    private void NavItem_Checked(object sender, RoutedEventArgs e)
    {
        if (_suppressNavSync)
        {
            return;
        }

        if (sender is not WpfRadioButton { Tag: string tag } || string.IsNullOrWhiteSpace(tag))
        {
            return;
        }

        // Avoid re-entrancy while SyncNavSelection sets IsChecked
        if (MainSettingsTabControl?.SelectedItem is TabItem current
            && string.Equals(current.Header?.ToString(), PageHeaderFromKey(NormalizePageKey(tag)), StringComparison.Ordinal))
        {
            return;
        }

        NavigateToPage(tag);
    }

    private void ToggleRailButton_Click(object sender, RoutedEventArgs e)
    {
        if (RailColumn is null || RightRailBorder is null)
        {
            return;
        }

        if (_isRailCollapsed)
        {
            RailColumn.Width = _savedRailWidth.Value > 0 ? _savedRailWidth : new GridLength(240);
            RightRailBorder.Visibility = WpfVisibility.Visible;
            _isRailCollapsed = false;
            ToggleRailButton.ToolTip = "折叠右侧栏";
            ToggleRailButton.Content = null; // template text is chevron
            UpdateRailToggleGlyph(collapsed: false);
        }
        else
        {
            if (RailColumn.Width.IsAbsolute && RailColumn.Width.Value > 0)
            {
                _savedRailWidth = RailColumn.Width;
            }
            else if (!RailColumn.Width.IsAbsolute)
            {
                _savedRailWidth = new GridLength(240);
            }

            RailColumn.Width = new GridLength(0);
            RightRailBorder.Visibility = WpfVisibility.Collapsed;
            _isRailCollapsed = true;
            ToggleRailButton.ToolTip = "展开右侧栏";
            UpdateRailToggleGlyph(collapsed: true);
        }
    }

    private void UpdateRailToggleGlyph(bool collapsed)
    {
        if (ToggleRailButton is null)
        {
            return;
        }

        ToggleRailButton.ApplyTemplate();
        if (ToggleRailButton.Template?.FindName("Chevron", ToggleRailButton) is System.Windows.Controls.TextBlock chevron)
        {
            chevron.Text = collapsed ? "›" : "‹";
        }
    }

    private void ExpandGestureAdvanced_Click(object sender, RoutedEventArgs e)
    {
        NavigateToPage("gestures", "GestureAdvancedSettingsExpander");
        Dispatcher.BeginInvoke(new Action(() =>
        {
            GestureAdvancedSettingsExpander.IsExpanded = true;
            ScrollToNamedElement("GestureAdvancedSettingsExpander");
        }), System.Windows.Threading.DispatcherPriority.Loaded);
    }

    private void FeatureMapCard_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string tag })
        {
            NavigateToPage(tag);
            e.Handled = true;
        }
    }

    private void ChangeGestureAction_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not SettingsViewModel viewModel)
        {
            return;
        }

        var pattern = (sender as FrameworkElement)?.Tag as string;
        if (string.IsNullOrWhiteSpace(pattern))
        {
            return;
        }

        viewModel.SelectedGestureBindingSelectionKey = pattern;
        NavigateToPage("bindings", "SectionGestureEditor");
        Dispatcher.BeginInvoke(new Action(() =>
        {
            try
            {
                ScrollToNamedElement("SectionGestureEditor");
                GestureActionEditorTitle.BringIntoView();
            }
            catch (InvalidOperationException)
            {
                GestureBindingPageScrollViewer.ScrollToVerticalOffset(
                    Math.Max(0, GestureBindingPageScrollViewer.ScrollableHeight * 0.45));
            }
        }), System.Windows.Threading.DispatcherPriority.Loaded);
        e.Handled = true;
    }

    private void ScrollToCustomGestureDesigner_Click(object sender, RoutedEventArgs e)
    {
        NavigateToPage("bindings", "SectionGestureDesigner");
        Dispatcher.BeginInvoke(new Action(() =>
        {
            try
            {
                var target = GestureDesignerPanel.TransformToAncestor(GestureBindingPageScrollViewer)
                    .Transform(new System.Windows.Point(0, 0));
                GestureBindingPageScrollViewer.ScrollToVerticalOffset(
                    Math.Max(0, GestureBindingPageScrollViewer.VerticalOffset + target.Y - 24));
            }
            catch (InvalidOperationException)
            {
                GestureBindingPageScrollViewer.ScrollToEnd();
            }
        }), System.Windows.Threading.DispatcherPriority.Loaded);
    }

}
