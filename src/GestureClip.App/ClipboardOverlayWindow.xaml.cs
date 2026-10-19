using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using GestureClip.App.ViewModels;
using GestureClip.Core.Abstractions;
using GestureClip.Core.Clipboard;
using GestureClip.Core.Settings;
using GestureClip.App.Services;

namespace GestureClip.App;

public partial class ClipboardOverlayWindow : Window
{
    private readonly ClipboardOverlayViewModel _viewModel;
    private readonly ISettingsService _settingsService;
    private readonly IConfirmationService _confirmationService;
    private readonly ClipboardPasteCoordinator _pasteCoordinator;
    private bool _alwaysVisible;
    private bool _isContextMenuOpen;
    private bool _hasBeenShown;

    public ClipboardOverlayWindow(
        ClipboardOverlayViewModel viewModel,
        ISettingsService settingsService,
        IConfirmationService confirmationService)
    {
        _viewModel = viewModel;
        _settingsService = settingsService;
        _confirmationService = confirmationService;
        _pasteCoordinator = new ClipboardPasteCoordinator(PasteCoreAsync);
        InitializeComponent();
        DataContext = _viewModel;
        PreviewKeyDown += Window_KeyDown;
        HistoryList.AlternationCount = 10;
        SetAlwaysVisible(_settingsService.Get(SettingKeys.ClipboardOverlayAlwaysVisible, false));
        IsVisibleChanged += ClipboardOverlayWindow_IsVisibleChanged;
    }

    private void ClipboardOverlayWindow_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is not true)
        {
            return;
        }

        if (_hasBeenShown)
        {
            BeginAnimation(OpacityProperty, null);
            Opacity = 1.0;
            return;
        }

        _hasBeenShown = true;
        var fade = new System.Windows.Media.Animation.DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(70))
        {
            EasingFunction = new System.Windows.Media.Animation.QuadraticEase
            {
                EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut
            }
        };
        BeginAnimation(OpacityProperty, fade);
    }

    public async Task<bool> LoadHistoryAsync(bool force = false)
    {
        var refreshed = await _viewModel.LoadAsync(force);
        if (refreshed)
        {
            ScrollToTopAndSelectLatest();
        }

        return refreshed;
    }

    public void FocusSearchBox()
    {
        SearchBox.Focus();
        SearchBox.SelectAll();
    }

    private void Window_Deactivated(object sender, EventArgs e)
    {
        if (_alwaysVisible || _isContextMenuOpen || _viewModel.IsShortcutHelpVisible)
        {
            return;
        }

        HideOverlayAndReleaseFocus();
    }

    private async void AlwaysVisibleButton_Click(object sender, RoutedEventArgs e)
    {
        SetAlwaysVisible(!_alwaysVisible);
        await _settingsService.SetAsync(SettingKeys.ClipboardOverlayAlwaysVisible, _alwaysVisible, CancellationToken.None);
    }

    private void SetAlwaysVisible(bool enabled)
    {
        _alwaysVisible = enabled;
        Topmost = enabled;
        if (AlwaysVisibleButton is not null)
        {
            AlwaysVisibleButton.Content = enabled ? "📍" : "📌";
            AlwaysVisibleButton.ToolTip = enabled ? "常显中：点击关闭（点外部不关）" : "点击开启常显";
            AlwaysVisibleButton.FontWeight = enabled ? FontWeights.SemiBold : FontWeights.Normal;
        }
    }

    private void ContextMenu_Opened(object sender, RoutedEventArgs e)
    {
        _isContextMenuOpen = true;
    }

    private void ContextMenu_Closed(object sender, RoutedEventArgs e)
    {
        _isContextMenuOpen = false;
    }

    private void ShortcutHelpButton_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.ToggleShortcutHelp();
    }

    private void ShortcutHelpBackdrop_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _viewModel.HideShortcutHelp();
        e.Handled = true;
    }

    private void ShortcutHelpCard_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // Prevent backdrop close when clicking the card itself.
        e.Handled = true;
    }
}
