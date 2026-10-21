using NTComponents.Charts.Core;
using NTComponents.Charts.Core.Series;
using SkiaSharp;

namespace NTComponents.Charts.Tests.ChartCore;

/// <summary>Data labels share one Material 3 renderer: label-medium text, 4dp corner, no shadow or border, and AA contrast on whatever they sit on.</summary>
public class DataLabelContrast_Tests : ChartTestBase {
    private (NTChart<Pt> Chart, NTBaseSeries<Pt> Series, SKSurface Surface, NTRenderContext Context) Setup() {
        var chart = RenderChart(Recording("a"));
        var surface = SKSurface.Create(new SKImageInfo(200, 200))!;
        surface.Canvas.Clear(SKColors.White);
        var context = new NTRenderContext {
            Canvas = surface.Canvas,
            DefaultFont = chart.Instance.DefaultFont,
            RegularFont = chart.Instance.RegularFont,
            Density = 1f,
            Info = new SKImageInfo(200, 200),
            PlotArea = new SKRect(0, 0, 200, 200),
            TextColor = SKColors.Black,
            TotalArea = new SKRect(0, 0, 200, 200)
        };
        return (chart.Instance, chart.Instance.Series.Single(), surface, context);
    }

    private static float MaxContrast(SKBitmap bitmap, SKRectI region, SKColor against) {
        var best = 0f;
        for (var x = region.Left; x < region.Right; x++) {
            for (var y = region.Top; y < region.Bottom; y++) {
                best = Math.Max(best, NTChartTokens.ContrastRatio(bitmap.GetPixel(x, y), against));
            }
        }

        return best;
    }

    [Fact]
    public void DrawDataLabel_WithoutBackground_UsesTheChartTextColorNotTheSeriesOnColor() {
        var (chart, series, surface, context) = Setup();
        using var _ = surface;

        context.DrawDataLabel(chart, series, 100, 100, 42m, new SKRect(0, 0, 200, 200), "{0:0}", showBackground: false);
        using var bitmap = Bitmap(surface);

        MaxContrast(bitmap, new SKRectI(60, 60, 140, 110), SKColors.White).Should().BeGreaterThanOrEqualTo(4.5f, "a label drawn straight on Surface needs its own AA contrast");
        CountColor(bitmap, chart.GetSeriesColor(series), 10).Should().Be(0, "no background is drawn");
    }

    [Fact]
    public void DrawDataLabel_WithBackground_TextHasAtLeastAaContrastOnTheBackground() {
        var (chart, series, surface, context) = Setup();
        using var _ = surface;

        context.DrawDataLabel(chart, series, 100, 100, 42m, new SKRect(0, 0, 200, 200), "{0:0}", backgroundColor: new SKColor(255, 235, 59));
        using var bitmap = Bitmap(surface);
        var badge = BoundsOf(bitmap, new SKColor(255, 235, 59), 10)!.Value;

        MaxContrast(bitmap, badge, new SKColor(255, 235, 59)).Should().BeGreaterThanOrEqualTo(4.5f, "light backgrounds must not receive the white on-color");
    }

    [Fact]
    public void DrawDataLabel_BadgeHasNoShadowOrBorderAndASmallCorner() {
        var (chart, series, surface, context) = Setup();
        using var _ = surface;
        var fill = new SKColor(0, 0, 255);

        context.DrawDataLabel(chart, series, 100, 100, 42m, new SKRect(0, 0, 200, 200), "{0:0}", backgroundColor: fill);
        using var bitmap = Bitmap(surface);
        var badge = BoundsOf(bitmap, fill, 10)!.Value;

        bitmap.GetPixel(badge.Right + 2, badge.Bottom + 2).Should().Be(SKColors.White, "there is no drop shadow");
        bitmap.GetPixel(badge.Left - 2, badge.MidY).Should().Be(SKColors.White, "there is no border");
        bitmap.GetPixel(badge.Left, badge.Top).Should().NotBe(fill, "the extra-small corner rounds the badge");
        bitmap.GetPixel(badge.Left + 4, badge.Top + 4).Should().Be(fill, "a 4dp corner leaves the rest of the edge straight");
        (badge.Bottom - badge.Top + 1).Should().BeInRange(18, 21, "label-medium line height plus 2dp block padding on each side");
    }

    [Fact]
    public void DrawDataLabel_UsesTheMediumWeightLabelFont() {
        var (chart, series, surface, context) = Setup();
        using var _ = surface;

        context.DrawDataLabel(chart, series, 100, 100, 42m, new SKRect(0, 0, 200, 200), "{0:0}");

        chart.ExtensionFont.Typeface.FontWeight.Should().Be(500);
        chart.ExtensionFont.Size.Should().Be(12f);
    }
}
