using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using GestureClip.App.Services;

namespace GestureClip.App;

public partial class MacroRecorderWindow : Window
{
    private readonly MacroRecorderService _recorder;
    private bool _closedAfterSave;

    public MacroRecorderWindow(MacroRecorderService recorder)
    {
        _recorder = recorder;
        InitializeComponent();
        DataContext = recorder;
        Recorder.Initialize(recorder);
    }

    protected override async void OnClosing(CancelEventArgs e)
    {
        if (!_closedAfterSave)
        {
            e.Cancel = true;
            await _recorder.StopAndSaveAsync();
            _closedAfterSave = true;
            _ = Dispatcher.BeginInvoke(new Action(Close));
        }
        base.OnClosing(e);
    }

}
