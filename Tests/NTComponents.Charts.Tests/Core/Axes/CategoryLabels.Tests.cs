using System.Collections;
using System.Reflection;
using Microsoft.AspNetCore.Components;
using NTComponents.Charts.Core;

namespace NTComponents.Charts.Tests.ChartCore;

/// <summary>
///     Categorical tick labels must follow the data: first-seen order, and rebuilt when the categories change even if
///     their count does not (a rolling window).
/// </summary>
public class NTXAxisOptions_CategoryLabels_Tests : ChartTestBase {
    private static readonly Pt[] JanToMar = [new("Jan", 0, 10m), new("Feb", 1, 20m), new("Mar", 2, 30m)];
    private static readonly Pt[] FebToApr = [new("Feb", 0, 20m), new("Mar", 1, 30m), new("Apr", 2, 40m)];

    private static RenderFragment NameChart(IEnumerable<Pt> data) => Combine(XAxis<string>(p => p.Name), Line(data, x: p => p.Name));

    private static List<string> TickLabels(IRenderedComponent<NTChart<Pt>> chart) {
        var axis = chart.Instance.XAxis;
        var ticks = (IEnumerable)axis.GetType().GetField("_cachedTicks", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(axis)!;
        return [.. ticks.Cast<object>().Select(t => (string)t.GetType().GetProperty("Label")!.GetValue(t)!)];
    }

    [Fact]
    public async Task Categories_AreLabelledInFirstSeenDataOrder() {
        var chart = RenderChart(NameChart([new("Mar", 0, 1m), new("Jan", 1, 2m), new("Feb", 2, 3m)]));

        await Paint(chart);

        TickLabels(chart).Should().Equal("Mar", "Jan", "Feb");
    }

    [Fact]
    public async Task RollingWindowWithTheSameCategoryCount_RelabelsTheAxis() {
        // An in-place data update plus Invalidate() does not re-render the chart, so only the axis tick cache key can notice.
        var data = new List<Pt>(JanToMar);
        var chart = RenderChart(NameChart(data));
        await Paint(chart);
        TickLabels(chart).Should().Equal("Jan", "Feb", "Mar");

        data.Clear();
        data.AddRange(FebToApr);
        var series = chart.FindComponent<NTLineSeries<Pt>>().Instance;
        await chart.InvokeAsync(series.Invalidate);
        await Paint(chart);

        TickLabels(chart).Should().Equal("Feb", "Mar", "Apr");
    }
}
