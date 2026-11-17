using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using GestureClip.Core.Settings;

namespace GestureClip.App;

/// <summary>HUD 动效强度，用户可在设置里选择，数值即持久化值。</summary>
public static class HudMotionLevels
{
    /// <summary>完整：全部流动层 + 扫描光，最高帧率。</summary>
    public const int Full = 0;

    /// <summary>均衡（默认）：降帧的流动层，扫描光保留。</summary>
    public const int Balanced = 1;

    /// <summary>极简：静态渐变，零动画开销。</summary>
    public const int Minimal = 2;

    public static int Normalize(int value) => value < Full ? Full : value > Minimal ? Minimal : value;
}

public partial class GestureOverlayWindow
{
    /// <summary>Breathing glow layers never need more than 15fps; they drift over seconds.</summary>
    private const int LiquidFrameRate = 15;

    private const int FlowFrameRateFull = 15;

    /// <summary>An 18–26s sweep moves barely a pixel per frame; 10fps is indistinguishable and cheaper.</summary>
    private const int FlowFrameRateBalanced = 10;

    /// <summary>Battery or "minimal" fallback: one step further down, still visibly flowing.</summary>
    private const int FlowFrameRateLowPower = 8;

    private static readonly TimeSpan TealFlowDuration = TimeSpan.FromSeconds(6);
    private static readonly TimeSpan VioletFlowDuration = TimeSpan.FromSeconds(8);

    /// <summary>Digit-block gradient band travels one full tile per cycle.</summary>
    private static readonly TimeSpan PixelFlowDuration = TimeSpan.FromSeconds(18);

    /// <summary>Secondary data stream drifts the other way at a slower, offset cadence.</summary>
    private static readonly TimeSpan AltFlowDuration = TimeSpan.FromSeconds(26);

    /// <summary>Vertical scan beam sweeps the panel roughly every nine seconds.</summary>
    private static readonly TimeSpan ScanBeamDuration = TimeSpan.FromSeconds(9);

    private int _motionLevel = HudMotionLevels.Balanced;
    private bool _onBattery;
    private string _appliedAccentHex = string.Empty;

    /// <summary>当前生效的动效强度（0 完整 / 1 均衡 / 2 极简）。</summary>
    public int MotionLevel => _motionLevel;

    /// <summary>True when animations are currently suppressed because the HUD is hidden.</summary>
    public bool IsMotionIdle { get; private set; } = true;

    /// <summary>Reads the persisted preference plus power state; call on construction and on every show.</summary>
    private void ApplyMotionPreferences()
    {
        if (_settingsService is not null)
        {
            _motionLevel = HudMotionLevels.Normalize(
                _settingsService.Get(SettingKeys.UiHudMotionLevel, HudMotionLevels.Balanced));
        }

        _onBattery = SystemParameters.PowerLineStatus == System.Windows.PowerLineStatus.Offline;
        ApplyAccentTint();
        UpdateLiquidMotion();
    }

    /// <summary>
    /// 用户选了强调色时，用强调色重建方块流动层；没选就保留 XAML 里的青紫默认配色。
    /// 每次显示都会比对一次，主题色改了不需要重启。
    /// </summary>
    private void ApplyAccentTint()
    {
        var hex = _settingsService?.Get(SettingKeys.UiAccentColor, string.Empty) ?? string.Empty;
        if (string.Equals(hex, _appliedAccentHex, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _appliedAccentHex = hex;
        if (!GestureClip.App.Services.AccentPalette.TryParse(hex, out var accent))
        {
            return;
        }

        HudPixelFlow.Background = HudAccentFlow.CreatePrimary(accent);
        HudPixelFlowAlt.Background = HudAccentFlow.CreateSecondary(accent);
        HudScanBeam.Background = HudAccentFlow.CreateScanBeam(accent);
    }

    private int CurrentFlowFrameRate => _onBattery
        ? FlowFrameRateLowPower
        : _motionLevel switch
        {
            HudMotionLevels.Full => FlowFrameRateFull,
            HudMotionLevels.Minimal => FlowFrameRateLowPower,
            _ => FlowFrameRateBalanced
        };

    private void UpdateLiquidMotion()
    {
        // 先清空所有动画：隐藏、切强度、切换到电池都走同一条取消路径。
        StopLiquidMotion();

        if (!IsVisible || !SystemParameters.ClientAreaAnimation)
        {
            IsMotionIdle = true;
            return;
        }

        IsMotionIdle = false;

        // 极简：只留静态渐变，一条动画都不跑。
        if (_motionLevel == HudMotionLevels.Minimal)
        {
            HudScanBeam.Visibility = Visibility.Collapsed;
            HudPixelFlowAlt.Visibility = Visibility.Collapsed;
            return;
        }

        HudScanBeam.Visibility = Visibility.Visible;
        HudPixelFlowAlt.Visibility = Visibility.Visible;

        AnimateLiquid(HudLiquidTeal.Background, 0.24, TealFlowDuration);
        AnimateLiquid(HudLiquidViolet.Background, -0.2, VioletFlowDuration);

        var flowFrameRate = CurrentFlowFrameRate;
        AnimateSweep(HudPixelFlow.Background, 1, PixelFlowDuration, flowFrameRate);

        // 电池供电时省掉斜向副流：主流动层已经撑住观感。
        if (!_onBattery)
        {
            AnimateSweep(HudPixelFlowAlt.Background, -1, AltFlowDuration, flowFrameRate);
        }

        AnimateSweep(HudScanBeam.Background, 1, ScanBeamDuration, Math.Max(flowFrameRate, 10));
    }

    private void StopLiquidMotion()
    {
        ResetBrushAnimation(HudLiquidTeal.Background);
        ResetBrushAnimation(HudLiquidViolet.Background);
        ResetBrushAnimation(HudPixelFlow.Background);
        ResetBrushAnimation(HudPixelFlowAlt.Background);
        ResetBrushAnimation(HudScanBeam.Background);
    }

    private static void ResetBrushAnimation(System.Windows.Media.Brush brush)
    {
        if (brush.RelativeTransform is TranslateTransform transform)
        {
            transform.BeginAnimation(TranslateTransform.XProperty, null);
            transform.BeginAnimation(TranslateTransform.YProperty, null);
        }
    }

    private void AnimateLiquid(System.Windows.Media.Brush brush, double distance, TimeSpan duration)
    {
        if (brush.RelativeTransform is not TranslateTransform transform)
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
    private static void AnimateSweep(
        System.Windows.Media.Brush brush, double distance, TimeSpan duration, int frameRate)
    {
        if (brush.RelativeTransform is not TranslateTransform transform)
        {
            return;
        }

        var animation = new DoubleAnimation(0, distance, duration)
        {
            RepeatBehavior = RepeatBehavior.Forever
        };
        Timeline.SetDesiredFrameRate(animation, frameRate);
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
