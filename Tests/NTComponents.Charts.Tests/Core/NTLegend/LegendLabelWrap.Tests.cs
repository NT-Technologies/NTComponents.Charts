using Microsoft.AspNetCore.Components;
using NTComponents.Charts.Core;
using SkiaSharp;

namespace NTComponents.Charts.Tests.ChartCore;

/// <summary>
///     Side and floating legends on any chart wrap long labels so the legend takes at most 30% of the canvas width.
/// </summary>
public class NTLegend_LabelWrap_Tests : ChartTestBase {
    private const string LongLabel = "Multiple body parts including the lower back, both knees, the right shoulder, and the cervical spine, reported together as one cumulative trauma claim";

    private static RenderFragment NoAxes() => Combine(XAxis<double>(p => p.X, Args(("Visible", false))), YAxis(Args(("Visible", false))));

    [Theory]
    [InlineData(LegendPosition.Right)]
    [InlineData(LegendPosition.Left)]
    [InlineData(LegendPosition.Floating)]
    public async Task LongLabelBesideOrOverTheChart_WrapsWithin30PercentOfTheCanvas(LegendPosition position) {
        var chart = RenderChart(Combine(NoAxes(), Legend(Args(("Position", position))), Recording("long", extra: Args(("Title", LongLabel))), Recording("short", extra: Args(("Title", "Short")))));
        using var surface = await Paint(chart);
        var legend = chart.FindComponent<NTLegend<Pt>>().Instance;

        var area = legend.LastDrawArea;
        area.Width.Should().BeInRange(1f, 0.3f * Width);
        var secondLine = new SKPoint(area.Left + NTChartTokens.LegendPadding + 10, area.Top + NTChartTokens.LegendPadding + NTChartTokens.LegendRowHeight + 4);
        legend.GetItemAtPoint(secondLine)!.Label.Should().Be(LongLabel, "the wrapped lines belong to the long item, not the next one");
    }

    // The points inside the legend that hit the long item.
    private static List<SKPoint> LongItemHits(NTLegend<Pt> legend) {
        var hits = new List<SKPoint>();
        var area = legend.LastDrawArea;
        for (var y = area.Top; y < area.Bottom; y += 2) {
            for (var x = area.Left; x < area.Right; x += 2) {
                if (legend.GetItemAtPoint(new SKPoint(x, y))?.Label == LongLabel) {
                    hits.Add(new SKPoint(x, y));
                }
            }
        }
        return hits;
    }

    [Theory]
    [InlineData(LegendPosition.Top)]
    [InlineData(LegendPosition.Bottom)]
    public async Task LongLabelAboveOrBelowTheChart_WrapsWithin30PercentOfTheCanvas(LegendPosition position) {
        var chart = RenderChart(Combine(NoAxes(), Legend(Args(("Position", position), ("MaxRows", 3))), Recording("long", extra: Args(("Title", LongLabel))), Recording("short", extra: Args(("Title", "Short")))));
        using var surface = await Paint(chart);

        var hits = LongItemHits(chart.FindComponent<NTLegend<Pt>>().Instance);

        (hits.Max(p => p.X) - hits.Min(p => p.X)).Should().BeLessThan(0.3f * Width);
        (hits.Max(p => p.Y) - hits.Min(p => p.Y)).Should().BeGreaterThan(NTChartTokens.LegendRowHeight, "the item grows to show its wrapped lines");
    }

    [Theory]
    [InlineData(LegendPosition.Top, 1)]
    [InlineData(LegendPosition.Bottom, 1)]
    [InlineData(LegendPosition.Bottom, 40)]
    public async Task WrappedLabelsAboveOrBelowTheChart_NeverGrowTheLegendPastMaxRows(LegendPosition position, int otherItems) {
        var others = Enumerable.Range(0, otherItems).Select(i => Recording($"s{i}", extra: Args(("Title", $"Body part {i}"))));
        var chart = RenderChart(Combine([NoAxes(), Legend(Args(("Position", position), ("MaxRows", 2))), Recording("long", extra: Args(("Title", LongLabel))), .. others]));
        using var surface = await Paint(chart);
        var legend = chart.FindComponent<NTLegend<Pt>>().Instance;

        legend.LastDrawArea.Height.Should().BeLessThanOrEqualTo((2 * NTChartTokens.LegendRowHeight) + NTChartTokens.LegendPadding + 0.01f);
        var hits = LongItemHits(legend);
        (hits.Max(p => p.Y) - hits.Min(p => p.Y)).Should().BeGreaterThan(NTChartTokens.LegendRowHeight, "the label still wraps to the lines that fit");
    }
}
