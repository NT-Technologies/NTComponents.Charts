using Microsoft.AspNetCore.Components;
using NTComponents.Charts.Core;
using NTComponents.Charts.Core.Axes;
using SkiaSharp;

namespace NTComponents.Charts.Tests.ChartCore;

/// <summary>
///     Long tick labels wrap or are shortened so the plot keeps its room, and hovering a shortened label shows its full
///     text in a tooltip.
/// </summary>
public class NTAxisOptions_LongLabels_Tests : ChartTestBase {
    private static readonly SKColor TooltipColor = SKColor.Parse("#123456");

    private static Pt[] Categories(int count, int nameLength) =>
        [.. Enumerable.Range(0, count).Select(i => new Pt($"{i} {string.Join(' ', Enumerable.Repeat("Vertebrae", nameLength / 10))}", i, 10m + i))];

    private static RenderFragment CategoryChart(Pt[] data, float? maxLabelWidth = 120f) =>
        Combine(XAxis<string>(p => p.Name, Args(("MaxLabelWidth", (object?)maxLabelWidth!))), Line(data, x: p => p.Name));

    private static RenderFragment HorizontalBars(Pt[] data, float? maxLabelWidth = 120f) =>
        Combine(XAxis<string>(p => p.Name), YAxis(Args(("MaxLabelWidth", (object?)maxLabelWidth!))), Bar(data, x: p => p.Name, extra: Args(("Orientation", NTChartOrientation.Horizontal))));

    private async Task<SKRect> PlotOf(RenderFragment content) {
        var chart = RenderChart(content);
        using var surface = await Paint(chart);
        return PlotArea(chart);
    }

    // The first point below the plot whose shortened X tick label reports its full text.
    private static (SKPoint Point, string Text)? FindShortenedXLabel(IRenderedComponent<NTChart<Pt>> chart) {
        var axis = (NTAxisOptions<Pt>)chart.Instance.XAxis;
        for (var y = (int)PlotArea(chart).Bottom; y < Height; y += 2) {
            for (var x = 0; x < Width; x += 2) {
                if (axis.GetTruncatedLabelAt(new SKPoint(x, y)) is { } text) {
                    return (new SKPoint(x, y), text);
                }
            }
        }
        return null;
    }

    [Fact]
    public async Task LongLabelsInWideSlots_WrapOntoASecondLineInsteadOfRotating() {
        var shortLabels = await PlotOf(CategoryChart(Categories(4, 10)));
        var longLabels = await PlotOf(CategoryChart(Categories(4, 60)));

        // A rotated 120px label would take about 90px; a second line takes one line spacing.
        (shortLabels.Bottom - longLabels.Bottom).Should().BeInRange(10, 20);
    }

    [Fact]
    public async Task LongLabelsInNarrowSlots_AreRotatedAndCappedAtMaxLabelWidth() {
        var long80 = await PlotOf(CategoryChart(Categories(30, 80)));
        var long160 = await PlotOf(CategoryChart(Categories(30, 160)));

        long160.Bottom.Should().BeApproximately(long80.Bottom, 2, "both label sets are shortened to the same width");
        long160.Height.Should().BeGreaterThan(Height / 2f);
    }

    [Fact]
    public async Task NullMaxLabelWidth_DrawsRotatedLabelsInFull() {
        var capped = await PlotOf(CategoryChart(Categories(30, 80)));
        var uncapped = await PlotOf(CategoryChart(Categories(30, 80), maxLabelWidth: null));

        uncapped.Bottom.Should().BeLessThan(capped.Bottom - 50);
    }

    [Fact]
    public async Task ShortenedXLabel_ReportsItsFullTextUnderThePointer() {
        var data = Categories(30, 80);
        var chart = RenderChart(CategoryChart(data));
        using var surface = await Paint(chart);

        var label = FindShortenedXLabel(chart);

        label.Should().NotBeNull();
        data.Select(p => p.Name).Should().Contain(label!.Value.Text);
    }

    [Fact]
    public async Task HoveringAShortenedLabel_ShowsAPlainTooltip() {
        ThemeColors["InverseSurface"] = "#123456";
        var chart = RenderChart(CategoryChart(Categories(30, 80)));
        using var surface = await Paint(chart);
        var point = FindShortenedXLabel(chart)!.Value.Point;

        await Move(chart, point.X, point.Y);
        await Paint(chart, surface: surface);
        using var bitmap = Bitmap(surface);

        BoundsOf(bitmap, TooltipColor, 4).Should().NotBeNull();
    }

    [Fact]
    public async Task HoveringALabelShownInFull_ShowsNoTooltip() {
        ThemeColors["InverseSurface"] = "#123456";
        var chart = RenderChart(CategoryChart(Categories(4, 10)));
        using var surface = await Paint(chart);
        var labelBand = PlotArea(chart).Bottom + 10;

        FindShortenedXLabel(chart).Should().BeNull();
        await Move(chart, Width / 2d, labelBand);
        await Paint(chart, surface: surface);
        using var bitmap = Bitmap(surface);

        BoundsOf(bitmap, TooltipColor, 4).Should().BeNull();
    }

    [Fact]
    public async Task MovingOffAShortenedLabel_HidesTheTooltip() {
        ThemeColors["InverseSurface"] = "#123456";
        var chart = RenderChart(CategoryChart(Categories(30, 80)));
        using var surface = await Paint(chart);
        var point = FindShortenedXLabel(chart)!.Value.Point;
        await Move(chart, point.X, point.Y);
        await Paint(chart, surface: surface);

        await Move(chart, Width / 2d, 20);
        await Paint(chart, surface: surface);
        using var bitmap = Bitmap(surface);

        BoundsOf(bitmap, TooltipColor, 4).Should().BeNull();
    }

    [Fact]
    public async Task LongHorizontalBarCategories_AreCappedAtMaxLabelWidth() {
        var capped = await PlotOf(HorizontalBars(Categories(4, 80)));
        var uncapped = await PlotOf(HorizontalBars(Categories(4, 80), maxLabelWidth: null));

        // Margin, the 120px label, and the axis padding.
        capped.Left.Should().BeLessThanOrEqualTo(8 + 120 + 10 + 1);
        uncapped.Left.Should().BeGreaterThan(capped.Left + 100);
    }

    [Fact]
    public async Task ShortenedYLabel_ReportsItsFullTextUnderThePointer() {
        var data = Categories(4, 80);
        var chart = RenderChart(HorizontalBars(data));
        using var surface = await Paint(chart);
        var axis = (NTAxisOptions<Pt>)chart.Instance.YAxis;
        var plot = PlotArea(chart);

        var texts = Enumerable.Range((int)plot.Top, (int)plot.Height)
            .Select(y => axis.GetTruncatedLabelAt(new SKPoint(plot.Left - 20, y)))
            .OfType<string>()
            .Distinct();

        texts.Should().BeEquivalentTo(data.Select(p => p.Name));
    }
}
