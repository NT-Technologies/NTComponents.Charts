using NTComponents.Charts.Core;
using NTComponents.Charts.Core.Axes;

namespace NTComponents.Charts.Tests.ChartCore;

public class NTChart_GetYRange_Tests : ChartTestBase {
    private const decimal Tolerance = 0.000001m;

    private static NTAxisOptions<Pt> Primary(IRenderedComponent<NTChart<Pt>> chart) => (NTAxisOptions<Pt>)chart.Instance.YAxis;

    private static NTAxisOptions<Pt> Secondary(IRenderedComponent<NTChart<Pt>> chart) => (NTAxisOptions<Pt>)chart.Instance.SecondaryYAxis!;

    [Fact]
    public void WithoutSeries_ReturnsUnitRange() {
        var chart = RenderChart(_ => { });

        chart.Instance.GetYRange(null, padded: true).Should().Be((0m, 1m));
    }

    [Fact]
    public void Unpadded_ReturnsDataExtents() {
        var chart = RenderChart(Line());

        chart.Instance.GetYRange(null, padded: false).Should().Be((10m, 40m));
    }

    [Fact]
    public void Padded_ExpandsBothSidesByRangePadding() {
        var chart = RenderChart(Line());

        var (min, max) = chart.Instance.GetYRange(null, padded: true);

        // 5% of the 30 unit span.
        min.Should().BeApproximately(8.5m, Tolerance);
        max.Should().BeApproximately(41.5m, Tolerance);
    }

    [Fact]
    public void CustomRangePadding_ScalesPaddedExtents() {
        var chart = RenderChart(Line(), parameters => parameters.Add(p => p.RangePadding, 0.2));

        var (min, max) = chart.Instance.GetYRange(null, padded: true);

        min.Should().BeApproximately(4m, Tolerance);
        max.Should().BeApproximately(46m, Tolerance);
    }

    [Fact]
    public void ConstantLargeValue_ExpandsByOnePercentOfTheValue() {
        var chart = RenderChart(Line([new("A", 0, 5m), new("B", 1, 5m)]));

        var (min, max) = chart.Instance.GetYRange(null, padded: false);

        min.Should().BeApproximately(4.95m, Tolerance);
        max.Should().BeApproximately(5.05m, Tolerance);
    }

    [Fact]
    public void ConstantSmallValue_ExpandsByOneUnit() {
        var chart = RenderChart(Line([new("A", 0, 0.5m), new("B", 1, 0.5m)]));

        chart.Instance.GetYRange(null, padded: false).Should().Be((-0.5m, 1.5m));
    }

    [Fact]
    public void MultipleSeries_ReturnsUnionOfSeriesExtents() {
        var other = new Pt[] { new("A", 0, -20m), new("B", 1, 15m) };
        var chart = RenderChart(Combine(Line(), Line(other)));

        chart.Instance.GetYRange(null, padded: false).Should().Be((-20m, 40m));
    }

    [Fact]
    public void HiddenSeries_DoesNotContributeToRange() {
        var hidden = new Pt[] { new("A", 0, 500m), new("B", 1, -500m) };
        var chart = RenderChart(Combine(Line(), Line(hidden, extra: Args(("Visible", false)))));

        chart.Instance.GetYRange(null, padded: false).Should().Be((10m, 40m));
    }

    [Fact]
    public void VerticalBarsAllPositive_FloorDefaultsToZeroAndOnlyTopIsPadded() {
        var chart = RenderChart(Bar());

        chart.Instance.GetYRange(null, padded: false).Should().Be((0m, 40m));
        var (min, max) = chart.Instance.GetYRange(null, padded: true);
        min.Should().Be(0m);
        max.Should().BeApproximately(42m, Tolerance);
    }

    [Fact]
    public void VerticalBarsWithValueAxisMinimum_UsesOverrideAsFloor() {
        var chart = RenderChart(Bar(extra: Args(("ValueAxisMinimum", (decimal?)5m))));

        chart.Instance.GetYRange(null, padded: false).Min.Should().Be(5m);
        var (min, max) = chart.Instance.GetYRange(null, padded: true);
        min.Should().Be(5m);
        max.Should().BeApproximately(40m + (35m * 0.05m), Tolerance);
    }

    [Fact]
    public void VerticalBarsWithNegativeValues_KeepDataMinimumAsFloor() {
        var chart = RenderChart(Bar([new("A", 0, -10m), new("B", 1, 30m)]));

        var (min, max) = chart.Instance.GetYRange(null, padded: true);

        min.Should().Be(-10m);
        max.Should().BeApproximately(32m, Tolerance);
    }

    [Fact]
    public void HorizontalBars_YAxisIsCategoryIndexRangeWithAtLeastHalfCategoryPadding() {
        var chart = RenderChart(Bar(x: p => p.Name, extra: Args(("Orientation", NTChartOrientation.Horizontal))));

        chart.Instance.GetYRange(null, padded: false).Should().Be((0m, 3m));
        var (min, max) = chart.Instance.GetYRange(null, padded: true);
        min.Should().BeApproximately(-0.5m, Tolerance);
        max.Should().BeApproximately(3.5m, Tolerance);
    }

    [Fact]
    public void AbsoluteMinimum_ClampsLowerBoundOfPaddedRange() {
        var data = new Pt[] { new("A", 0, -5m), new("B", 1, 10m) };
        var chart = RenderChart(Combine(YAxis(Args(("AbsoluteMinimum", (decimal?)0m))), Line(data)));

        var (min, max) = chart.Instance.GetYRange(Primary(chart), padded: true);

        min.Should().Be(0m);
        max.Should().BeApproximately(10.75m, Tolerance);
    }

    [Fact]
    public void AbsoluteMinimumAboveAllData_KeepsMinimumAndOpensRangeUpward() {
        var data = new Pt[] { new("A", 0, -5m), new("B", 1, -2m) };
        var chart = RenderChart(Combine(YAxis(Args(("AbsoluteMinimum", (decimal?)0m))), Line(data)));

        var (min, max) = chart.Instance.GetYRange(Primary(chart), padded: false);

        min.Should().Be(0m);
        max.Should().Be(1m);
    }

    [Fact]
    public void SecondaryYAxis_RangesAreComputedIndependentlyPerAxis() {
        var secondaryData = new Pt[] { new("A", 0, 1000m), new("B", 1, 2000m) };
        var chart = RenderChart(Combine(
            YAxis(),
            YAxis(),
            Line(),
            Line(secondaryData, extra: Args(("UseSecondaryYAxis", true)))));

        chart.Instance.GetYRange(Primary(chart), padded: false).Should().Be((10m, 40m));
        chart.Instance.GetYRange(Secondary(chart), padded: false).Should().Be((1000m, 2000m));
    }

    [Fact]
    public void SecondaryYAxisWithoutSeries_FallsBackToUnitRange() {
        var chart = RenderChart(Combine(YAxis(), YAxis(), Line()));

        chart.Instance.GetYRange(Secondary(chart), padded: true).Should().Be((0m, 1m));
    }

    [Fact]
    public async Task XViewWindow_RestrictsYRangeToVisiblePointsPlusOverscan() {
        var data = Enumerable.Range(0, 10).Select(i => new Pt($"P{i}", i, i == 9 ? 1000m : i * 10m)).ToArray();
        var chart = RenderChart(Combine(XAxis<double>(p => p.X), Line(data)));

        await chart.InvokeAsync(() => chart.Instance.SetViewXRange(1, 2));

        // The window [1,2] plus one overscan point on each side covers indexes 0..3, excluding the spike at x=9.
        var (min, max) = chart.Instance.GetYRange(null, padded: false);
        min.Should().Be(0m);
        max.Should().Be(30m);
    }

    [Fact]
    public async Task ResetView_RestoresFullYRangeAfterXWindowing() {
        var data = Enumerable.Range(0, 10).Select(i => new Pt($"P{i}", i, i == 9 ? 1000m : i * 10m)).ToArray();
        var chart = RenderChart(Combine(XAxis<double>(p => p.X), Line(data)));
        await chart.InvokeAsync(() => chart.Instance.SetViewXRange(1, 2));

        await chart.InvokeAsync(chart.Instance.ResetView);

        chart.Instance.GetYRange(null, padded: false).Should().Be((0m, 1000m));
    }
}
