using System.ComponentModel;
using System.Windows;
using GestureClip.Core.Abstractions;

namespace GestureClip.App;

public partial class MainWindow : Window
{
    private readonly IAppLifecycleService _appLifecycleService;

    public MainWindow(IAppLifecycleService appLifecycleService)
    {
        _appLifecycleService = appLifecycleService;
        InitializeComponent();
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
