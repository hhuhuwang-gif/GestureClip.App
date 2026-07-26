using System.Windows.Media;
using GestureClip.App.Services;
using Xunit;

namespace GestureClip.Tests.App;

public sealed class AppThemeServiceTests
{
    [Theory]
    [InlineData(null, AppThemePreference.Light)]
    [InlineData("", AppThemePreference.Light)]
    [InlineData("Light", AppThemePreference.Light)]
    [InlineData("light", AppThemePreference.Light)]
    [InlineData("Dark", AppThemePreference.Dark)]
    [InlineData("dark", AppThemePreference.Dark)]
    [InlineData("System", AppThemePreference.System)]
    [InlineData("Auto", AppThemePreference.System)]
    [InlineData(" system ", AppThemePreference.System)]
    [InlineData("garbage", AppThemePreference.Light)]
    public void ParsePreference_maps_setting_values(string? raw, AppThemePreference expected)
    {
        Assert.Equal(expected, AppThemeService.ParsePreference(raw));
    }

    [Theory]
    [InlineData(AppThemePreference.Light, "Light")]
    [InlineData(AppThemePreference.Dark, "Dark")]
    [InlineData(AppThemePreference.System, "System")]
    public void PreferenceToSettingValue_round_trips(AppThemePreference preference, string expected)
    {
        Assert.Equal(expected, AppThemeService.PreferenceToSettingValue(preference));
        Assert.Equal(preference, AppThemeService.ParsePreference(expected));
    }
}

public sealed class AccentPaletteTests
{
    [Fact]
    public void TryParse_accepts_hex_and_rejects_blank_or_invalid()
    {
        Assert.True(AccentPalette.TryParse("#8B5CF6", out var color));
        Assert.Equal(Color.FromRgb(0x8B, 0x5C, 0xF6), color);

        Assert.False(AccentPalette.TryParse(null, out _));
        Assert.False(AccentPalette.TryParse("", out _));
        Assert.False(AccentPalette.TryParse("   ", out _));
        Assert.False(AccentPalette.TryParse("not-a-color", out _));
    }

    [Fact]
    public void Blend_endpoints_return_inputs()
    {
        var from = Color.FromRgb(0x10, 0x20, 0x30);
        var to = Color.FromRgb(0xF0, 0xE0, 0xD0);

        Assert.Equal(from, AccentPalette.Blend(from, to, 0));
        Assert.Equal(to, AccentPalette.Blend(from, to, 1));
    }

    [Fact]
    public void Lighten_and_darken_move_toward_white_and_black()
    {
        var accent = Color.FromRgb(0x80, 0x80, 0x80);

        var lighter = AccentPalette.Lighten(accent, 0.5);
        Assert.True(lighter.R > accent.R && lighter.G > accent.G && lighter.B > accent.B);

        var darker = AccentPalette.Darken(accent, 0.5);
        Assert.True(darker.R < accent.R && darker.G < accent.G && darker.B < accent.B);
    }

    [Fact]
    public void BrightAccent_lightens_only_in_dark_mode()
    {
        var accent = Color.FromRgb(0x8B, 0x5C, 0xF6);

        Assert.Equal(accent, AccentPalette.BrightAccent(accent, isDark: false));

        var dark = AccentPalette.BrightAccent(accent, isDark: true);
        Assert.NotEqual(accent, dark);
        Assert.True(dark.R >= accent.R && dark.G >= accent.G && dark.B >= accent.B);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BuildColorOverrides_covers_accent_keys(bool isDark)
    {
        var overrides = AccentPalette.BuildColorOverrides(Color.FromRgb(0x16, 0xA3, 0x4A), isDark);

        string[] expectedKeys =
        [
            "ColorPrimaryBright",
            "ColorPrimarySoft",
            "ColorFocusRing",
            "ColorAccentSoft",
            "ColorControlSelected",
            "ColorListSelectedHover",
            "ColorBrandSky"
        ];
        foreach (var key in expectedKeys)
        {
            Assert.True(overrides.ContainsKey(key), $"missing {key}");
        }

        // Soft variants must stay translucent in light mode so glass surfaces keep depth.
        if (!isDark)
        {
            Assert.True(overrides["ColorPrimarySoft"].A < 0xFF);
            Assert.True(overrides["ColorFocusRing"].A < 0xFF);
        }
    }
}
