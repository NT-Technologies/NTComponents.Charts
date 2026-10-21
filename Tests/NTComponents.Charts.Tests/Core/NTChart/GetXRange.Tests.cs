using NTComponents.Charts.Core;

namespace NTComponents.Charts.Tests.ChartCore;

public class NTChart_GetXRange_Tests : ChartTestBase {
    private const double Tolerance = 1e-9;

    [Fact]
    public void WithoutSeries_ReturnsUnitRange() {
        var chart = RenderChart(_ => { });

        var (min, max) = chart.Instance.GetXRange(null, padded: true);

        min.Should().Be(0);
        max.Should().Be(1);
    }

    [Fact]
    public void NumericAxisUnpadded_ReturnsDataExtents() {
        var chart = RenderChart(Combine(XAxis<double>(p => p.X), Line()));

        var (min, max) = chart.Instance.GetXRange(null, padded: false);

        min.Should().BeApproximately(0, Tolerance);
        max.Should().BeApproximately(3, Tolerance);
    }

    [Fact]
    public void NumericAxisPadded_ExpandsBothSidesByRangePadding() {
        var chart = RenderChart(Combine(XAxis<double>(p => p.X), Line()));

        var (min, max) = chart.Instance.GetXRange(null, padded: true);

        // RangePadding defaults to 5% of the 0..3 span.
        min.Should().BeApproximately(-0.15, Tolerance);
        max.Should().BeApproximately(3.15, Tolerance);
    }

    [Fact]
    public void CustomRangePadding_ScalesPaddedExtents() {
        var chart = RenderChart(Combine(XAxis<double>(p => p.X), Line()), parameters => parameters.Add(p => p.RangePadding, 0.1));

        var (min, max) = chart.Instance.GetXRange(null, padded: true);

        min.Should().BeApproximately(-0.3, Tolerance);
        max.Should().BeApproximately(3.3, Tolerance);
    }

    [Fact]
    public void ZeroRangePadding_PaddedRangeEqualsDataExtents() {
        var chart = RenderChart(Combine(XAxis<double>(p => p.X), Line()), parameters => parameters.Add(p => p.RangePadding, 0.0));

        var padded = chart.Instance.GetXRange(null, padded: true);
        var unpadded = chart.Instance.GetXRange(null, padded: false);

        padded.Should().Be(unpadded);
    }

    [Fact]
    public void SinglePointUnpadded_ExpandsToNonDegenerateRange() {
        var chart = RenderChart(Combine(XAxis<double>(p => p.X), Line([new("A", 5, 1m)])));

        var (min, max) = chart.Instance.GetXRange(null, padded: false);

        // |5| > 1 so the delta is 1% of the value.
        min.Should().BeApproximately(4.95, Tolerance);
        max.Should().BeApproximately(5.05, Tolerance);
    }

    [Fact]
    public void SinglePointPadded_RangeStaysCenteredOnThePoint() {
        var chart = RenderChart(Combine(XAxis<double>(p => p.X), Line([new("A", 5, 1m)])));

        var (min, max) = chart.Instance.GetXRange(null, padded: true);

        min.Should().BeLessThan(5);
        max.Should().BeGreaterThan(5);
        ((min + max) / 2).Should().BeApproximately(5, 1e-6);
    }

    [Fact]
    public void MultipleSeries_ReturnsUnionOfSeriesExtents() {
        var second = new Pt[] { new("E", 7, 1m), new("F", 10, 2m) };
        var chart = RenderChart(Combine(XAxis<double>(p => p.X), Line(), Line(second)));

        var (min, max) = chart.Instance.GetXRange(null, padded: false);

        min.Should().BeApproximately(0, Tolerance);
        max.Should().BeApproximately(10, Tolerance);
    }

    [Fact]
    public void HiddenSeries_DoesNotContributeToRange() {
        var hidden = new Pt[] { new("E", 100, 1m), new("F", 200, 2m) };
        var chart = RenderChart(Combine(XAxis<double>(p => p.X), Line(), Line(hidden, extra: Args(("Visible", false)))));

        var (min, max) = chart.Instance.GetXRange(null, padded: false);

        min.Should().BeApproximately(0, Tolerance);
        max.Should().BeApproximately(3, Tolerance);
    }

    [Fact]
    public void CategoricalAxisUnpadded_ReturnsIndexRange() {
        var chart = RenderChart(Line(x: p => p.Name));

        var (min, max) = chart.Instance.GetXRange(null, padded: false);

        min.Should().Be(0);
        max.Should().Be(3);
    }

    [Fact]
    public void CategoricalAxisPadded_ExpandsByPaddingPercentageOfIndexSpan() {
        var chart = RenderChart(Line(x: p => p.Name));

        var (min, max) = chart.Instance.GetXRange(null, padded: true);

        min.Should().BeApproximately(-0.15, Tolerance);
        max.Should().BeApproximately(3.15, Tolerance);
    }

    [Fact]
    public void CategoricalAxisWithOneCategory_CentersTheCategory() {
        var chart = RenderChart(Line([new("Only", 0, 1m)], x: p => p.Name));

        chart.Instance.GetXRange(null, padded: false).Should().Be((-0.5d, 0.5d));
        var (min, max) = chart.Instance.GetXRange(null, padded: true);
        min.Should().BeApproximately(-0.55, Tolerance);
        max.Should().BeApproximately(0.55, Tolerance);
    }

    [Fact]
    public void CategoricalAxisWithVerticalBars_PadsAtLeastHalfACategorySoEdgeBarsAreNotClipped() {
        var chart = RenderChart(Bar(x: p => p.Name));

        var (min, max) = chart.Instance.GetXRange(null, padded: true);

        min.Should().BeApproximately(-0.5, Tolerance);
        max.Should().BeApproximately(3.5, Tolerance);
    }

    [Fact]
    public void CategoricalAxisWithVerticalBars_UnpaddedRangeHasNoEdgePadding() {
        var chart = RenderChart(Bar(x: p => p.Name));

        chart.Instance.GetXRange(null, padded: false).Should().Be((0d, 3d));
    }

    [Fact]
    public async Task ExplicitViewRange_OverridesDataExtentsWithoutPadding() {
        var chart = RenderChart(Combine(XAxis<double>(p => p.X), Line()));

        await chart.InvokeAsync(() => chart.Instance.SetViewXRange(1, 2));

        chart.Instance.GetXRange(null, padded: true).Should().Be((1d, 2d));
    }

    [Fact]
    public async Task ReversedViewRange_IsNormalizedToAscendingOrder() {
        var chart = RenderChart(Combine(XAxis<double>(p => p.X), Line()));

        await chart.InvokeAsync(() => chart.Instance.SetViewXRange(2, 1));

        chart.Instance.GetXRange(null, padded: true).Should().Be((1d, 2d));
    }

    [Fact]
    public async Task ZeroWidthViewRange_ExpandsAroundTheValue() {
        var chart = RenderChart(Combine(XAxis<double>(p => p.X), Line()));

        await chart.InvokeAsync(() => chart.Instance.SetViewXRange(1, 1));

        // |1| is not greater than 1, so the fallback delta is a full unit.
        chart.Instance.GetXRange(null, padded: true).Should().Be((0d, 2d));
    }

    [Fact]
    public async Task ResetView_RestoresDataDrivenRange() {
        var chart = RenderChart(Combine(XAxis<double>(p => p.X), Line()));
        await chart.InvokeAsync(() => chart.Instance.SetViewXRange(1, 2));

        await chart.InvokeAsync(chart.Instance.ResetView);

        var (min, max) = chart.Instance.GetXRange(null, padded: true);
        min.Should().BeApproximately(-0.15, Tolerance);
        max.Should().BeApproximately(3.15, Tolerance);
    }

    [Fact]
    public void HorizontalBarsAllPositive_ValueAxisStartsAtZeroWithoutLeftPadding() {
        var data = new Pt[] { new("A", 0, 10m), new("B", 1, 50m) };
        var chart = RenderChart(Bar(data, x: p => p.Name, extra: Args(("Orientation", NTChartOrientation.Horizontal))));

        var (min, max) = chart.Instance.GetXRange(null, padded: true);

        min.Should().Be(0);
        max.Should().BeApproximately(52.5, Tolerance);
    }

    [Fact]
    public void HorizontalBarsAllNegative_ValueAxisEndsAtZeroWithoutRightPadding() {
        var data = new Pt[] { new("A", 0, -10m), new("B", 1, -50m) };
        var chart = RenderChart(Bar(data, x: p => p.Name, extra: Args(("Orientation", NTChartOrientation.Horizontal))));

        var (min, max) = chart.Instance.GetXRange(null, padded: true);

        min.Should().BeApproximately(-52, Tolerance);
        max.Should().Be(-10);
    }

    [Fact]
    public void HorizontalBarsWithValueAxisMinimum_UsesOverrideAsLowerBound() {
        var data = new Pt[] { new("A", 0, 10m), new("B", 1, 50m) };
        var chart = RenderChart(Bar(data, x: p => p.Name, extra: Args(("Orientation", NTChartOrientation.Horizontal), ("ValueAxisMinimum", (decimal?)5m))));

        chart.Instance.GetXRange(null, padded: false).Min.Should().Be(5);
    }
}
