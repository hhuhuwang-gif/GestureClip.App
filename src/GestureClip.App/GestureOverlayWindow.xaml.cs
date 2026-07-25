using System.Windows;
using System.Windows.Input;
using GestureClip.App.ViewModels;

namespace GestureClip.App;

public partial class GestureOverlayWindow : Window
{
    public GestureOverlayWindow(GestureOverlayViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void BindActionBar_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is GestureOverlayViewModel vm && vm.BindUnboundGestureCommand.CanExecute(null))
        {
            vm.BindUnboundGestureCommand.Execute(null);
            e.Handled = true;
        }
    }
}
