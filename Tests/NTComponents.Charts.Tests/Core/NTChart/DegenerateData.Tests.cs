using Microsoft.AspNetCore.Components;
using NTComponents.Charts.Core;
using NTComponents.Charts.Core.Axes;
using SkiaSharp;

namespace NTComponents.Charts.Tests.ChartCore;

public class NTChart_DegenerateData_Tests : ChartTestBase {
    private static float Dark(SKBitmap bitmap, int x, int y) => InkAt(bitmap, x, y, SKColors.Black, 0);

    private static int CountRuns(IEnumerable<float> coverage) {
        var runs = 0;
        var inside = false;
        foreach (var value in coverage) {
            if (value > 0.2f && !inside) {
                runs++;
            }
            inside = value > 0.2f;
        }
        return runs;
    }

    private async Task<(int XTicks, int YTicks, SKRect Plot)> TickCounts(RenderFragment content) {
        var chart = RenderChart(content);
        using var surface = await Paint(chart);
        using var bitmap = Bitmap(surface);
        var plot = PlotArea(chart);
        var xTicks = CountRuns(Enumerable.Range(0, bitmap.Width).Select(x => Dark(bitmap, x, (int)plot.Bottom + 3)));
        var yTicks = CountRuns(Enumerable.Range(0, bitmap.Height).Select(y => Dark(bitmap, (int)plot.Left - 3, y)));
        return (xTicks, yTicks, plot);
    }

    [Fact]
    public async Task EmptySeries_PaintsWithoutErrorsAndKeepsTheAxesFrame() {
        var chart = RenderChart(Combine(XAxis<double>(p => p.X), YAxis(), Line([])));

        using var surface = await Paint(chart);
        using var bitmap = Bitmap(surface);
        var plot = PlotArea(chart);

        plot.Width.Should().BeGreaterThan(100);
        InkAt(bitmap, (int)plot.MidX, (int)plot.Bottom, SKColors.Black).Should().BeGreaterThan(0.4f);
        chart.Instance.GetXRange(null, true).Should().Be((0d, 1d));
        chart.Instance.GetYRange(null, true).Should().Be((0m, 1m));
    }

    [Fact]
    public async Task ChartWithoutSeries_PaintsBackgroundOnly() {
        var chart = RenderChart(_ => { });

        using var surface = await Paint(chart);
        using var bitmap = Bitmap(surface);

        bitmap.GetPixel(10, 10).Should().Be(SKColors.White);
        PlotArea(chart).Should().Be(new SKRect(8, 8, Width - 8, Height - 8));
    }


    [Fact]
    public void ConstantYValuesPadded_ExpandToAReadableWindow() {
        var chart = RenderChart(Combine(XAxis<double>(p => p.X), YAxis(), Line([new("A", 0, 7m), new("B", 1, 7m)])));

        var (min, max) = chart.Instance.GetYRange(null, padded: true);

        (max - min).Should().BeGreaterThan(0.05m);
        min.Should().BeLessThan(7m);
        max.Should().BeGreaterThan(7m);
    }

    [Fact]
    public void SingleNumericXPadded_ExpandsToAReadableWindow() {
        var chart = RenderChart(Combine(XAxis<double>(p => p.X), Line([new("A", 5, 1m)])));

        var (min, max) = chart.Instance.GetXRange(null, padded: true);

        (max - min).Should().BeGreaterThan(0.05);
    }

    [Fact]
    public async Task ConstantYValues_ProduceAFiniteRangeWithTicks() {
        var data = new Pt[] { new("A", 0, 5m), new("B", 1, 5m), new("C", 2, 5m) };
        var (_, yTicks, plot) = await TickCounts(Combine(XAxis<double>(p => p.X), YAxis(), Line(data)));

        yTicks.Should().BeGreaterThan(1);
        plot.Height.Should().BeGreaterThan(100);
    }

    [Fact]
    public async Task AllZeroValues_AreScaledInsideThePlot() {
        var data = new Pt[] { new("A", 0, 0m), new("B", 1, 0m) };
        var chart = RenderChart(Combine(XAxis<double>(p => p.X), YAxis(), Line(data)));
        using var surface = await Paint(chart);
        var plot = PlotArea(chart);

        var y = chart.Instance.ScaleY(0m, plot);

        y.Should().BeInRange(plot.Top, plot.Bottom);
    }

    [Fact]
    public async Task NegativeOnlyValues_AreScaledInsideThePlotWithNiceTicks() {
        var data = new Pt[] { new("A", 0, -50m), new("B", 1, -10m) };
        var chart = RenderChart(Combine(XAxis<double>(p => p.X), YAxis(), Line(data)));
        using var surface = await Paint(chart);
        var plot = PlotArea(chart);

        chart.Instance.ScaleY(-50m, plot).Should().BeGreaterThan(chart.Instance.ScaleY(-10m, plot));
        chart.Instance.ScaleY(-50m, plot).Should().BeLessThanOrEqualTo(plot.Bottom);
        chart.Instance.ScaleY(-10m, plot).Should().BeGreaterThanOrEqualTo(plot.Top);
    }

    [Fact]
    public async Task LogarithmicAxes_WithNonPositiveData_StillPaint() {
        var data = new Pt[] { new("A", 0, -5m), new("B", 1, 0m), new("C", 2, 100m) };
        var chart = RenderChart(Combine(XAxis<double>(p => p.X, Args(("Scale", NTAxisScale.Logarithmic))), YAxis(Args(("Scale", NTAxisScale.Logarithmic))), Line(data)));

        var act = () => Paint(chart);

        await act.Should().NotThrowAsync();
        var plot = PlotArea(chart);
        float.IsFinite(chart.Instance.ScaleX(0, plot)).Should().BeTrue();
        float.IsFinite(chart.Instance.ScaleY(-5m, plot)).Should().BeTrue();
    }

    [Fact]
    public async Task VeryLargeValues_DoNotBreakScaling() {
        var data = new Pt[] { new("A", 0, 1e20m), new("B", 1, 5e20m) };
        var chart = RenderChart(Combine(XAxis<double>(p => p.X), YAxis(), Line(data)));
        using var surface = await Paint(chart);
        var plot = PlotArea(chart);

        float.IsFinite(chart.Instance.ScaleY(3e20m, plot)).Should().BeTrue();
        chart.Instance.ScaleY(5e20m, plot).Should().BeLessThan(chart.Instance.ScaleY(1e20m, plot));
    }

    [Fact]
    public async Task SingleDateValue_PaintsADateAxisWithOneTick() {
        var day = new DateTime(2024, 5, 5);
        var data = new Pt[] { new("A", 0, 1m, day), new("B", 1, 2m, day) };
        var (xTicks, _, _) = await TickCounts(Combine(XAxis<DateTime>(p => p.Date), YAxis(), Line(data, x: p => p.Date)));

        xTicks.Should().BeGreaterThan(0);
        xTicks.Should().BeLessThan(20, "the zero-width range is widened by 1% of the tick value, i.e. about a week either side");
    }

    [Fact]
    public async Task SingleCategory_PaintsOneTickAtTheCategory() {
        var (xTicks, _, _) = await TickCounts(Combine(YAxis(), Line([new("Only", 0, 3m)], x: p => p.Name)));

        xTicks.Should().Be(1);
    }

    [Fact]
    public async Task ManyCategories_AreThinnedToReadableTickCountAndStayInsideThePlot() {
        var data = Enumerable.Range(0, 400).Select(i => new Pt($"Category {i:000}", i, i)).ToArray();
        var (xTicks, _, plot) = await TickCounts(Combine(YAxis(), Line(data, x: p => p.Name)));

        xTicks.Should().BeInRange(5, 100);
        plot.Width.Should().BeGreaterThan(100);
    }

    [Fact]
    public async Task ManyPoints_PaintAndHitTestWithoutErrors() {
        var data = Enumerable.Range(0, 5000).Select(i => new Pt($"P{i}", i, (decimal)Math.Sin(i / 50d) * 100)).ToArray();
        var chart = RenderChart(Combine(XAxis<double>(p => p.X), YAxis(), Tooltip(), Line(data)));
        using var surface = await Paint(chart);

        await Move(chart, 300, 150);
        var act = () => Paint(chart, surface: surface);

        await act.Should().NotThrowAsync();
    }
}
