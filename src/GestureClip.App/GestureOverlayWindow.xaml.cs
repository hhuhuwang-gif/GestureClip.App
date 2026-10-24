using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using GestureClip.App.ViewModels;
using Color = System.Windows.Media.Color;

namespace GestureClip.App;

public partial class GestureOverlayWindow : Window
{
    private static readonly Color SuccessColor = Color.FromRgb(0x22, 0xC5, 0x5E);
    private static readonly Color FailColor = Color.FromRgb(0xEF, 0x44, 0x44);

    public GestureOverlayWindow(GestureOverlayViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        HudLiquidTeal.Background = HudLiquidTeal.Background.Clone();
        HudLiquidViolet.Background = HudLiquidViolet.Background.Clone();
        HudPixelFlow.Background = HudPixelFlow.Background.Clone();
        HudPixelFlowAlt.Background = HudPixelFlowAlt.Background.Clone();
        HudScanBeam.Background = HudScanBeam.Background.Clone();
        IsVisibleChanged += (_, _) => UpdateLiquidMotion();
    }

    /// <summary>Green pulse when the gesture triggered an action, red fade when it was unbound.</summary>
    public void PlayTrailFeedback(bool success)
    {
        var brush = new SolidColorBrush(success ? SuccessColor : FailColor);
        brush.Freeze();
        TrailFeedbackLine.Stroke = brush;

        var pulse = new DoubleAnimationUsingKeyFrames { FillBehavior = FillBehavior.Stop };
        pulse.KeyFrames.Add(new EasingDoubleKeyFrame(success ? 0.9 : 0.7, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(90))));
        pulse.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(success ? 320 : 460))));
        TrailFeedbackLine.BeginAnimation(OpacityProperty, pulse);
    }

}
