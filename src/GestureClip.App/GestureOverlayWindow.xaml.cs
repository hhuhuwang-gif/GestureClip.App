using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using GestureClip.App.ViewModels;
using Color = System.Windows.Media.Color;

namespace GestureClip.App;

public partial class GestureOverlayWindow : Window
{
    private static readonly Color SuccessColor = Color.FromRgb(0x22, 0xC5, 0x5E);

    public GestureOverlayWindow(GestureOverlayViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    /// <summary>Green pulse when the gesture triggered an action.</summary>
    public void PlayTrailFeedback()
    {
        var brush = new SolidColorBrush(SuccessColor);
        brush.Freeze();
        TrailFeedbackLine.Stroke = brush;

        var pulse = new DoubleAnimationUsingKeyFrames { FillBehavior = FillBehavior.Stop };
        pulse.KeyFrames.Add(new EasingDoubleKeyFrame(0.9, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(90))));
        pulse.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(320))));
        TrailFeedbackLine.BeginAnimation(OpacityProperty, pulse);
    }
}
