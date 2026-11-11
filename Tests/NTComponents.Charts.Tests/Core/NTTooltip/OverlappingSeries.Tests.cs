using Microsoft.AspNetCore.Components;
using NTComponents.Charts.Core;

namespace NTComponents.Charts.Tests.ChartCore;

/// <summary>
///     Hovering a line, area or point series also lists the other such series with a point within OverlapDistance of the hovered
///     point, so overlapping points are never hidden; ShowAllSeriesAtX lists every series with a value at the hovered X.
/// </summary>
public class NTTooltip_OverlappingSeries_Tests : ChartTestBase {
    private static readonly Pt[] Shared = [new("a", 0, 10m), new("b", 1, 40m), new("c", 2, 20m)];
    private static readonly Pt[] Apart = [new("a", 0, 30m), new("b", 1, 5m), new("c", 2, 35m)];
    private static readonly Pt[] Elsewhere = [new("a", 0, 15m), new("c", 2, 25m)];
    // 0.5 below the hovered 40 at the same X: a few pixels away, so it reads as the same spot.
    private static readonly Pt[] Near = [new("a", 0, 25m), new("b", 1, 39.5m), new("c", 2, 22m)];

    private IRenderedComponent<NTChart<Pt>> Lines(params (string Name, object Value)[] tooltip) => RenderChart(Combine(
        XAxis<double>(p => p.X),
        YAxis(),
        Tooltip(Args(tooltip)),
        Line(Shared, extra: Args(("Title", "First"))),
        Series<NTPointSeries<Pt>>(Shared, extra: Args(("Title", "Overlapping"))),
        Line(Apart, extra: Args(("Title", "Apart"))),
        Series<NTAreaSeries<Pt>>(Elsewhere, extra: Args(("Title", "No point at X"))),
        Line(Near, extra: Args(("Title", "Near")))));

    private static async Task<List<string>> HoverLabels(IRenderedComponent<NTChart<Pt>> chart, double x, decimal y) {
        using var surface = await Paint(chart);
        var plot = PlotArea(chart);
        await Move(chart, chart.Instance.ScaleX(x, plot), chart.Instance.ScaleY(y, plot));
        // The chart resolves the hovered point on the next frame.
        await Paint(chart, surface: surface);
        var instance = chart.Instance;
        instance.HoveredSeries.Should().NotBeNull("the pointer must land on a data point");
        var tooltip = chart.FindComponent<NTTooltip<Pt>>().Instance;
        var info = await chart.InvokeAsync(() => tooltip.GetTooltipInfo(instance.HoveredSeries!, instance.HoveredDataPoint!));
        return [.. info.Lines.Select(line => line.Label)];
    }

    private static float PixelsBetween(IRenderedComponent<NTChart<Pt>> chart, decimal a, decimal b) =>
        Math.Abs(chart.Instance.ScaleY(a, PlotArea(chart)) - chart.Instance.ScaleY(b, PlotArea(chart)));

    [Fact]
    public async Task PointsWithinTheOverlapDistance_AllAppearInChartOrder() {
        var chart = Lines();

        var labels = await HoverLabels(chart, 1, 40m);

        PixelsBetween(chart, 40m, 39.5m).Should().BeLessThan(8f, "the Near point must sit inside the default 8dp margin");
        labels.Should().Equal("First", "Overlapping", "Near");
    }

    [Fact]
    public async Task ZeroOverlapDistance_MergesOnlyPointsAtTheIdenticalSpot() {
        var chart = Lines(("OverlapDistance", 0f));

        var near = await HoverLabels(chart, 1, 39.5m);
        var shared = await HoverLabels(chart, 0, 10m);

        near.Should().Equal(["Near"], "First and Overlapping are a few pixels away");
        shared.Should().Equal("First", "Overlapping");
    }

    [Fact]
    public async Task APointOnItsOwn_ShowsOnlyItsSeries() {
        var chart = Lines();

        var labels = await HoverLabels(chart, 1, 5m);

        labels.Should().Equal("Apart");
    }

    [Fact]
    public async Task ShowAllSeriesAtX_ListsEverySeriesWithAValueAtThatX() {
        var chart = Lines(("ShowAllSeriesAtX", true));

        var labels = await HoverLabels(chart, 1, 5m);

        labels.Should().Equal("First", "Overlapping", "Apart", "Near");
    }
}
