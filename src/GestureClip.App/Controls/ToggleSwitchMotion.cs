using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using CheckBox = System.Windows.Controls.CheckBox;

namespace GestureClip.App.Controls;

/// <summary>Animates only the switch thumb, honoring Windows' animation preference.</summary>
public static class ToggleSwitchMotion
{
    private const double ThumbTravel = 18;
    private static readonly TimeSpan TransitionTime = TimeSpan.FromMilliseconds(160);

    public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached(
        "IsEnabled", typeof(bool), typeof(ToggleSwitchMotion), new PropertyMetadata(false, OnEnabledChanged));

    public static bool GetIsEnabled(DependencyObject target) => (bool)target.GetValue(IsEnabledProperty);
    public static void SetIsEnabled(DependencyObject target, bool value) => target.SetValue(IsEnabledProperty, value);

    private static void OnEnabledChanged(DependencyObject target, DependencyPropertyChangedEventArgs e)
    {
        if (target is not CheckBox toggle) return;
        if ((bool)e.NewValue)
        {
            toggle.Loaded += OnLoaded;
            toggle.Checked += OnCheckedChanged;
            toggle.Unchecked += OnCheckedChanged;
            toggle.Indeterminate += OnCheckedChanged;
            UpdateThumb(toggle, false);
            return;
        }
        toggle.Loaded -= OnLoaded;
        toggle.Checked -= OnCheckedChanged;
        toggle.Unchecked -= OnCheckedChanged;
        toggle.Indeterminate -= OnCheckedChanged;
    }

    private static void OnLoaded(object sender, RoutedEventArgs e) => UpdateThumb((CheckBox)sender, false);

    private static void OnCheckedChanged(object sender, RoutedEventArgs e)
    {
        var toggle = (CheckBox)sender;
        UpdateThumb(toggle, toggle.IsLoaded && toggle.IsVisible && SystemParameters.ClientAreaAnimation);
    }

    private static void UpdateThumb(CheckBox toggle, bool animate)
    {
        if (!toggle.IsLoaded) return;
        if (toggle.Template?.FindName("Thumb", toggle) is not FrameworkElement thumb
            || thumb.RenderTransform is not TranslateTransform transform) return;

        // WPF freezes template Freezables; give each thumb its own mutable animation target.
        if (transform.IsFrozen)
        {
            transform = transform.Clone();
            thumb.RenderTransform = transform;
        }

        var destination = toggle.IsChecked == true ? ThumbTravel : 0;
        var current = transform.X;
        transform.BeginAnimation(TranslateTransform.XProperty, null);
        transform.X = destination;
        if (!animate) return;

        transform.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(current, destination, TransitionTime)
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
            FillBehavior = FillBehavior.Stop
        }, HandoffBehavior.SnapshotAndReplace);
    }
}
