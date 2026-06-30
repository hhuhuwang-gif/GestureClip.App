using System.Windows;
using GestureClip.App.ViewModels;

namespace GestureClip.App;

public partial class GestureOverlayWindow : Window
{
    public GestureOverlayWindow(GestureOverlayViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
