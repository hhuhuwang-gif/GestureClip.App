using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace GestureClip.App;

public partial class GestureOverlayWindow
{
    private const int LiquidFrameRate = 15;
    private const int FlowFrameRate = 15;

    private static readonly TimeSpan TealFlowDuration = TimeSpan.FromSeconds(6);
    private static readonly TimeSpan VioletFlowDuration = TimeSpan.FromSeconds(8);

    /// <summary>Digit-block gradient band travels one full tile per cycle.</summary>
    private static readonly TimeSpan PixelFlowDuration = TimeSpan.FromSeconds(18);

    /// <summary>Secondary data stream drifts the other way at a slower, offset cadence.</summary>
    private static readonly TimeSpan AltFlowDuration = TimeSpan.FromSeconds(26);

    /// <summary>Vertical scan beam sweeps the panel roughly every nine seconds.</summary>
    private static readonly TimeSpan ScanBeamDuration = TimeSpan.FromSeconds(9);

    private void UpdateLiquidMotion()
    {
        AnimateLiquid(HudLiquidTeal.Background, 0.24, TealFlowDuration);
        AnimateLiquid(HudLiquidViolet.Background, -0.2, VioletFlowDuration);
        AnimateSweep(HudPixelFlow.Background, 1, PixelFlowDuration);
        AnimateSweep(HudPixelFlowAlt.Background, -1, AltFlowDuration);
        AnimateSweep(HudScanBeam.Background, 1, ScanBeamDuration);
    }

    private void AnimateLiquid(System.Windows.Media.Brush brush, double distance, TimeSpan duration)
    {
        var transform = (TranslateTransform)brush.RelativeTransform;
        transform.BeginAnimation(TranslateTransform.XProperty, null);
        transform.BeginAnimation(TranslateTransform.YProperty, null);
        if (!IsVisible || !SystemParameters.ClientAreaAnimation)
        {
            return;
        }

        transform.BeginAnimation(TranslateTransform.XProperty, CreateLiquidAnimation(distance, duration));
        transform.BeginAnimation(TranslateTransform.YProperty, CreateLiquidAnimation(-distance, duration));
    }

    /// <summary>
    /// Translates a repeating gradient by exactly one period so the digit-block
    /// colour band flows across the pixel lattice without a visible seam.
    /// </summary>
    private void AnimateSweep(System.Windows.Media.Brush brush, double distance, TimeSpan duration)
    {
        if (brush.RelativeTransform is not TranslateTransform transform)
        {
            return;
        }

        transform.BeginAnimation(TranslateTransform.XProperty, null);
        if (!IsVisible || !SystemParameters.ClientAreaAnimation)
        {
            return;
        }

        var animation = new DoubleAnimation(0, distance, duration)
        {
            RepeatBehavior = RepeatBehavior.Forever
        };
        Timeline.SetDesiredFrameRate(animation, FlowFrameRate);
        transform.BeginAnimation(TranslateTransform.XProperty, animation);
    }

    private static DoubleAnimation CreateLiquidAnimation(double distance, TimeSpan duration)
    {
        var animation = new DoubleAnimation(0, distance, duration)
        {
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever,
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
        };
        Timeline.SetDesiredFrameRate(animation, LiquidFrameRate);
        return animation;
    }
}
