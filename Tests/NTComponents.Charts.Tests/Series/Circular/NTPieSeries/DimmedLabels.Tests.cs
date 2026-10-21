using NTComponents.Charts.Core;
using NTComponents.Charts.Core.Series;
using SkiaSharp;

namespace NTComponents.Charts.Tests.Series.Circular;

/// <summary>Pie labels stay legible while their slice is de-emphasized because another slice is hovered.</summary>
public class PieDimmedLabels_Tests : CircularChartTestBase {
    // A bare pie chart has a 5px margin, so the plot area is (5,5)-(395,295): center (200,150), radius 145.
    private static readonly SKPoint Center = new(200, 150);

    [Fact]
    public async Task DimmedSlice_LabelKeepsAtLeastAaContrastAgainstTheDimmedFill() {
        var cut = RenderChart<NTPieSeries<Item>>(Items(1, 1, 2), new() { ["ShowDataLabels"] = true });
        using var surface = NewSurface();
        await PaintAsync(cut, surface);
        var hover = Polar(Center, 100, -45);
        await HoverAsync(cut, surface, hover.X, hover.Y);
        surface.Canvas.Clear(SKColors.White);

        await PaintAsync(cut, surface);

        var fill = PixelAt(surface, Polar(Center, 125, 20).X, Polar(Center, 125, 20).Y);
        fill.Red.Should().BeGreaterThan(120, "the slice is de-emphasized to the 38% opacity");
        var label = Polar(Center, 145 * 0.67f, 45);
        var best = 0f;
        for (var dx = -14; dx <= 14; dx++) {
            for (var dy = -14; dy <= 14; dy++) {
                best = Math.Max(best, NTChartTokens.ContrastRatio(PixelAt(surface, label.X + dx, label.Y + dy), fill));
            }
        }

        best.Should().BeGreaterThanOrEqualTo(4.5f, "label text must stay readable on a dimmed slice");
    }

    [Fact]
    public void LegibleTextColor_OnADimmedFill_NeverFallsBelowAa() {
        var surface = SKColors.White;
        foreach (var slice in new[] { SKColors.Red, SKColors.Green, new SKColor(84, 90, 146), SKColors.Yellow }) {
            var dimmed = slice.WithAlpha((byte)(255 * NTChartTokens.DeEmphasisOpacity));
            var text = NTSeriesGeometry.LegibleTextColor(SKColors.White, dimmed, surface);
            var blended = new SKColor((byte)(255 + ((slice.Red - 255) * NTChartTokens.DeEmphasisOpacity)), (byte)(255 + ((slice.Green - 255) * NTChartTokens.DeEmphasisOpacity)), (byte)(255 + ((slice.Blue - 255) * NTChartTokens.DeEmphasisOpacity)));

            NTChartTokens.ContrastRatio(text, blended).Should().BeGreaterThanOrEqualTo(4.5f, $"{slice} dimmed to 38%");
        }
    }
}
