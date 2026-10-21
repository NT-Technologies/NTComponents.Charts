using NTComponents.Charts.Core;
using NTComponents.Charts.Tests.Rendering;
using SkiaSharp;

namespace NTComponents.Charts.Tests.ChartCore;

public class PaletteContrast_Tests {
    public static TheoryData<string> Schemes => ["light", "dark"];

    private static Dictionary<string, string?> Scheme(string name) => name == "dark" ? GoldenThemeColors.Dark() : GoldenThemeColors.Create();

    private static SKColor Resolve(Dictionary<string, string?> scheme, NTColor color) => SKColor.Parse(scheme[color.ToString()]!);

    [Theory]
    [MemberData(nameof(Schemes))]
    public void DefaultPalette_EveryEntryHasNonTextContrastAgainstSurface(string name) {
        var scheme = Scheme(name);
        var surface = Resolve(scheme, NTColor.Surface);

        foreach (var (background, _) in new NTChart<Pt>().Palette) {
            NTChartTokens.ContrastRatio(Resolve(scheme, background), surface).Should().BeGreaterThanOrEqualTo(3f, $"{background} must be distinguishable from Surface in the {name} scheme");
        }
    }

    [Theory]
    [MemberData(nameof(Schemes))]
    public void DefaultPalette_EveryTextPairHasTextContrastAgainstItsBackground(string name) {
        var scheme = Scheme(name);

        foreach (var (background, text) in new NTChart<Pt>().Palette) {
            NTChartTokens.ContrastRatio(Resolve(scheme, text), Resolve(scheme, background)).Should().BeGreaterThanOrEqualTo(4.5f, $"{text} on {background} must be readable in the {name} scheme");
        }
    }

    [Fact]
    public void DefaultPalette_UsesPrimaryTertiarySecondaryThenExtendedRoles() {
        new NTChart<Pt>().Palette.Select(p => p.Background).Should().Equal(NTColor.Primary, NTColor.Tertiary, NTColor.Secondary, NTColor.Assert, NTColor.Warning, NTColor.Success);
    }

    [Fact]
    public void ContrastingTextColor_PicksTheHigherContrastOfBlackAndWhite() {
        NTChartTokens.ContrastingTextColor(SKColor.Parse("#FFFFFF")).Should().Be(SKColors.Black);
        NTChartTokens.ContrastingTextColor(SKColor.Parse("#000000")).Should().Be(SKColors.White);
        NTChartTokens.ContrastingTextColor(SKColor.Parse("#E45756")).Should().Be(SKColors.Black, "mid-tone red has more contrast with black");
    }

    [Fact]
    public void TextBaseline_CentersRobotoInLine() {
        using var font = new SKFont(NTRobotoFonts.Regular, 14);
        var metrics = font.Metrics;
        var expected = 100f + ((20f - (metrics.Descent - metrics.Ascent)) / 2f) - metrics.Ascent;

        NTChartTokens.TextBaseline(100f, 20f, 14f).Should().BeApproximately(expected, 0.35f);
    }
}
