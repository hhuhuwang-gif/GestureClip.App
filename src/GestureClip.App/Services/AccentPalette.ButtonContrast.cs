using Color = System.Windows.Media.Color;

namespace GestureClip.App.Services;

public static partial class AccentPalette
{
    private const double WhiteTextMinimumContrast = 4.5;
    private const double ContrastLuminanceOffset = 0.05;
    private const double WhiteLuminance = 1;
    private const double ChannelMaximum = 255;
    private const double SrgbLinearThreshold = 0.04045;
    private const double LinearSrgbThreshold = 0.0031308;
    private const double SrgbLinearScale = 12.92;
    private const double SrgbOffset = 0.055;
    private const double SrgbScale = 1.055;
    private const double SrgbExponent = 2.4;
    private const double RedLuminanceWeight = 0.2126;
    private const double GreenLuminanceWeight = 0.7152;
    private const double BlueLuminanceWeight = 0.0722;
    private const double ButtonHoverDarkening = 0.12;
    private const double ButtonPressedDarkening = 0.24;

    /// <summary>Opaque accent fill with WCAG AA contrast for normal-size white button text.</summary>
    public static Color ButtonFill(Color accent)
    {
        var red = Linearize(accent.R);
        var green = Linearize(accent.G);
        var blue = Linearize(accent.B);
        var luminance = RedLuminanceWeight * red + GreenLuminanceWeight * green + BlueLuminanceWeight * blue;
        var maximumLuminance = (WhiteLuminance + ContrastLuminanceOffset) / WhiteTextMinimumContrast
            - ContrastLuminanceOffset;
        if (luminance <= maximumLuminance)
        {
            return Color.FromRgb(accent.R, accent.G, accent.B);
        }

        // Scale in linear light to retain hue; floor channel values so quantization cannot reduce contrast.
        var scale = maximumLuminance / luminance;
        return Color.FromRgb(EncodeChannel(red * scale), EncodeChannel(green * scale), EncodeChannel(blue * scale));
    }

    private static double Linearize(byte channel)
    {
        var srgb = channel / ChannelMaximum;
        return srgb <= SrgbLinearThreshold
            ? srgb / SrgbLinearScale
            : Math.Pow((srgb + SrgbOffset) / SrgbScale, SrgbExponent);
    }

    private static byte EncodeChannel(double linear)
    {
        var srgb = linear <= LinearSrgbThreshold
            ? linear * SrgbLinearScale
            : SrgbScale * Math.Pow(linear, 1 / SrgbExponent) - SrgbOffset;
        return (byte)Math.Floor(srgb * ChannelMaximum);
    }
}
