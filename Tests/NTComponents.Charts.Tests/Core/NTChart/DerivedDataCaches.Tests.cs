using NTComponents.Charts.Core;

namespace NTComponents.Charts.Tests.ChartCore;

/// <summary>
///     Values the chart derives from series data (date-time X detection, the categorical X index) are cached until the
///     data changes. These tests pin down that a data change is always reflected.
/// </summary>
public class NTChart_DerivedDataCaches_Tests : ChartTestBase {
    private static readonly DateTime Start = new(2026, 1, 1);
    private static readonly Pt[] Dated = [new("A", 0, 10m, Start), new("B", 1, 40m, Start.AddDays(1)), new("C", 2, 20m, Start.AddDays(2))];

    [Fact]
    public void XValueSwitchedToDates_ReportsADateTimeAxis() {
        var chart = RenderChart(Line(Dated));
        chart.Instance.IsXAxisDateTime.Should().BeFalse();

        chart.Render(p => p.Add(c => c.ChildContent, Line(Dated, x: p => p.Date)));

        chart.Instance.IsXAxisDateTime.Should().BeTrue();
    }

    [Fact]
    public void DataReplaced_RebuildsTheCategoricalXIndex() {
        var chart = RenderChart(Line([new("A", 0, 1m), new("B", 1, 2m)], x: p => p.Name));
        chart.Instance.GetScaledXValue("B").Should().Be(1);

        chart.Render(p => p.Add(c => c.ChildContent, Line([new("0", 0, 1m), new("A", 1, 2m), new("B", 2, 3m)], x: p => p.Name)));

        chart.Instance.GetScaledXValue("B").Should().Be(2);
        chart.Instance.GetAllXValues().Should().Equal("0", "A", "B");
    }
}
