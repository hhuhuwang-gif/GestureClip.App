using System.ComponentModel;
using System.Windows;
using GestureClip.App.Services;
using GestureClip.App.ViewModels;

namespace GestureClip.App;

public partial class SettingsWindow : Window
{
    private readonly AppLifecycleService _appLifecycleService;

    public SettingsWindow(SettingsViewModel viewModel, AppLifecycleService appLifecycleService)
    {
        _appLifecycleService = appLifecycleService;
        InitializeComponent();
        DataContext = viewModel;
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_appLifecycleService.IsExplicitExit)
        {
            e.Cancel = true;
            Hide();
            return;
        }

        base.OnClosing(e);
    }
}
