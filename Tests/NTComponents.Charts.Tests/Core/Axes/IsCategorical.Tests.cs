using Microsoft.AspNetCore.Components;
using NTComponents.Charts.Core;

namespace NTComponents.Charts.Tests.ChartCore;

/// <summary>
///     The category-type part of <c>IsCategorical</c> is computed once per axis type; the horizontal-bar part must still
///     follow the series as they change.
/// </summary>
public class NTXAxisOptions_IsCategorical_Tests : ChartTestBase {
    private static RenderFragment NameAxisWithBar(bool visible, NTChartOrientation orientation) =>
        Combine(XAxis<string>(p => p.Name), Bar(x: p => p.Name, extra: Args(("Orientation", orientation), ("Visible", visible))));

    [Fact]
    public void HidingTheHorizontalBars_MakesTheAxisCategoricalAgain() {
        var chart = RenderChart(NameAxisWithBar(visible: true, NTChartOrientation.Horizontal));
        chart.Instance.XAxis.IsCategorical.Should().BeFalse();

        chart.Render(p => p.Add(c => c.ChildContent, NameAxisWithBar(visible: false, NTChartOrientation.Horizontal)));

        chart.Instance.XAxis.IsCategorical.Should().BeTrue();
    }

    [Fact]
    public void TurningTheBarsVertical_MakesTheAxisCategoricalAgain() {
        var chart = RenderChart(NameAxisWithBar(visible: true, NTChartOrientation.Horizontal));
        chart.Instance.XAxis.IsCategorical.Should().BeFalse();

        chart.Render(p => p.Add(c => c.ChildContent, NameAxisWithBar(visible: true, NTChartOrientation.Vertical)));

        chart.Instance.XAxis.IsCategorical.Should().BeTrue();
    }
}
