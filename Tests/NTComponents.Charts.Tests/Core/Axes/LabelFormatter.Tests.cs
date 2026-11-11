using System.Collections;
using System.Reflection;
using Microsoft.AspNetCore.Components;
using NTComponents.Charts.Core;

namespace NTComponents.Charts.Tests.ChartCore;

/// <summary>A <c>LabelFormatter</c> replaces the axis tick text (and the tooltip text that reads like the axis) with the caller's own.</summary>
public class LabelFormatter_Tests : ChartTestBase {
    private static readonly Pt[] HeatCells = [new("Jan", 0, 1m), new("Feb", 1, 1m), new("Jan", 0, 2m), new("Feb", 1, 2m), new("Jan", 0, 3m), new("Feb", 1, 3m)];

    private static readonly Func<decimal, string> MonthName = v => new DateTime(2024, (int)v, 1).ToString("MMM yyyy", System.Globalization.CultureInfo.InvariantCulture);

    private static List<(object Value, string Label)> Ticks(object axis) {
        var ticks = (IEnumerable)axis.GetType().GetField("_cachedTicks", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(axis)!;
        return [.. ticks.Cast<object>().Select(t => (t.GetType().GetProperty("Value")!.GetValue(t)!, (string)t.GetType().GetProperty("Label")!.GetValue(t)!))];
    }

    private static RenderFragment HeatMap(Func<decimal, string>? formatter = null) => Combine(
        XAxis<string>(p => p.Name),
        YAxis(formatter is null ? null : Args(("LabelFormatter", formatter))),
        Series<NTHeatMapSeries<Pt>>(HeatCells, x: p => p.Name, extra: Args(("WeightSelector", (Func<Pt, decimal>)(p => p.Y)))));

    [Fact]
    public async Task CategoricalXAxis_LabelsEachCategoryWithTheFormatter() {
        var chart = RenderChart(Combine(XAxis<string>(p => p.Name, Args(("LabelFormatter", (Func<string, string>)(name => $"<{name}>")))), Line(x: p => p.Name)));

        await Paint(chart);

        Ticks(chart.Instance.XAxis).Select(t => t.Label).Should().Equal("<A>", "<B>", "<C>", "<D>");
    }

    [Fact]
    public async Task NumericXAxis_PassesTicksAsTheAxisType() {
        var received = new List<int>();
        var chart = RenderChart(Combine(XAxis<int>(p => (int)p.X, Args(("LabelFormatter", (Func<int, string>)(v => { received.Add(v); return $"#{v}"; })))), Line()));

        await Paint(chart);

        received.Should().NotBeEmpty();
        Ticks(chart.Instance.XAxis).Should().OnlyContain(t => t.Label == $"#{Convert.ToInt32(t.Value)}");
    }

    [Fact]
    public async Task XAxisFormatter_AlsoFormatsTheTooltipHeader() {
        var chart = RenderChart(Combine(XAxis<string>(p => p.Name, Args(("LabelFormatter", (Func<string, string>)(name => name.ToLowerInvariant())))), Line(x: p => p.Name)));

        await Paint(chart);

        chart.Instance.XAxis.FormatValue("B", forTooltip: true).Should().Be("b");
    }

    [Fact]
    public async Task YAxisFormatter_ReplacesTheTickLabels() {
        var chart = RenderChart(Combine(YAxis(Args(("LabelFormatter", (Func<decimal, string>)(v => $"{v:0} pts")))), Line()));

        await Paint(chart);

        Ticks(chart.Instance.YAxis).Should().OnlyContain(t => t.Label == $"{(decimal)t.Value:0} pts");
    }

    [Fact]
    public async Task HeatMapYAxis_PutsOneTickOnEachRow() {
        var chart = RenderChart(HeatMap());

        await Paint(chart);

        Ticks(chart.Instance.YAxis).Select(t => (decimal)t.Value).Should().Equal(1m, 2m, 3m);
    }

    [Fact]
    public async Task HeatMapYAxisFormatter_LabelsTheRowsAndTheTooltip() {
        var chart = RenderChart(HeatMap(MonthName));

        await Paint(chart);
        var series = chart.FindComponent<NTHeatMapSeries<Pt>>().Instance;
        var tooltip = await chart.InvokeAsync(() => ((ISeries)series).GetTooltipInfo(HeatCells[2]));

        Ticks(chart.Instance.YAxis).Select(t => t.Label).Should().Equal("Jan 2024", "Feb 2024", "Mar 2024");
        tooltip.Lines.Should().Contain(l => l.Label == "Y" && l.Value == "Feb 2024");
    }
}
