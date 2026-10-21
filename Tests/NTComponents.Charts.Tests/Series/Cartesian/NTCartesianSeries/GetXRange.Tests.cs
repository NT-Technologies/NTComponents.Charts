using NTComponents.Charts.Core;

namespace NTComponents.Charts.Tests.Series.Cartesian;

public class GetXRange_Tests : CartesianChartTestBase {
    [Fact]
    public void WithEmptyData_ReturnsNull() {
        var cut = RenderChart(XAxisKind.Int, SeriesFragment<NTLineSeries<Pt>>([]));

        SeriesOf<NTLineSeries<Pt>>(cut).GetXRange().Should().BeNull();
    }

    [Fact]
    public void WithSinglePoint_ReturnsCollapsedRange() {
        var cut = RenderChart(XAxisKind.Int, SeriesFragment<NTLineSeries<Pt>>([new Pt(7, 1m)]));

        SeriesOf<NTLineSeries<Pt>>(cut).GetXRange().Should().Be((7d, 7d));
    }

    [Fact]
    public void WithUnsortedData_ReturnsSortedExtents() {
        var cut = RenderChart(XAxisKind.Int, SeriesFragment<NTLineSeries<Pt>>([new Pt(5, 1m), new Pt(-3, 2m), new Pt(9, 3m), new Pt(0, 4m)]));

        SeriesOf<NTLineSeries<Pt>>(cut).GetXRange().Should().Be((-3d, 9d));
    }

    [Fact]
    public void WithFractionalDoubleX_ReturnsExactValues() {
        var cut = RenderChart(XAxisKind.Double, SeriesFragment<NTLineSeries<Pt>>([new Pt(0.25, 1m), new Pt(2.75, 2m), new Pt(1.5, 3m)]));

        SeriesOf<NTLineSeries<Pt>>(cut).GetXRange().Should().Be((0.25d, 2.75d));
    }

    [Fact]
    public void WithDecimalBackedX_ConvertsToDouble() {
        var cut = RenderChart(XAxisKind.Double, SeriesFragment<NTLineSeries<Pt>>([new Pt(1.5m, 1m), new Pt(4.25m, 2m)]));

        SeriesOf<NTLineSeries<Pt>>(cut).GetXRange().Should().Be((1.5d, 4.25d));
    }

    [Fact]
    public void WithDateTimeX_ReturnsTicks() {
        var first = new DateTime(2024, 1, 1);
        var last = new DateTime(2024, 1, 5);
        var cut = RenderChart(XAxisKind.DateTime, SeriesFragment<NTLineSeries<Pt>>([new Pt(last, 1m), new Pt(first, 2m)]));

        SeriesOf<NTLineSeries<Pt>>(cut).GetXRange().Should().Be(((double)first.Ticks, (double)last.Ticks));
    }

    [Fact]
    public void WithCategoricalX_ReturnsIndexRangeOfSortedDistinctCategories() {
        var cut = RenderChart(XAxisKind.Default, SeriesFragment<NTLineSeries<Pt>>([new Pt("C", 1m), new Pt("A", 2m), new Pt("B", 3m), new Pt("A", 4m)]));

        SeriesOf<NTLineSeries<Pt>>(cut).GetXRange().Should().Be((0d, 2d));
    }

    [Fact]
    public void WithNaNXValue_IgnoresTheInvalidPoint() {
        var cut = RenderChart(XAxisKind.Double, SeriesFragment<NTLineSeries<Pt>>([new Pt(1.0, 10m), new Pt(double.NaN, 20m), new Pt(3.0, 30m)]));

        SeriesOf<NTLineSeries<Pt>>(cut).GetXRange().Should().Be((1d, 3d));
        ChartXRange(cut.Instance).Should().Be((1d, 3d));
    }

    [Fact]
    public void WithDuplicateXValues_ReturnsSameExtentsAsDistinct() {
        var cut = RenderChart(XAxisKind.Int, SeriesFragment<NTLineSeries<Pt>>([new Pt(2, 1m), new Pt(2, 5m), new Pt(4, 3m), new Pt(4, 9m)]));

        SeriesOf<NTLineSeries<Pt>>(cut).GetXRange().Should().Be((2d, 4d));
    }

    [Fact]
    public void AfterReplacingData_RecomputesRange() {
        var cut = RenderChart(XAxisKind.Int, SeriesFragment<NTLineSeries<Pt>>([new Pt(1, 1m), new Pt(2, 2m)]));
        var series = SeriesOf<NTLineSeries<Pt>>(cut);
        series.GetXRange().Should().Be((1d, 2d));

        ReRender(cut, new ChartSetup(), SeriesFragment<NTLineSeries<Pt>>([new Pt(10, 1m), new Pt(20, 2m)]));

        series.GetXRange().Should().Be((10d, 20d));
    }

    [Fact]
    public void AfterInPlaceMutationAndInvalidate_RecomputesRange() {
        var data = new List<Pt> { new(1, 1m), new(2, 2m) };
        var cut = RenderChart(XAxisKind.Int, SeriesFragment<NTLineSeries<Pt>>(data));
        var series = SeriesOf<NTLineSeries<Pt>>(cut);
        series.GetXRange().Should().Be((1d, 2d));

        data.Add(new Pt(50, 3m));
        series.Invalidate();

        series.GetXRange().Should().Be((1d, 50d));
    }

    [Fact]
    public void ChartRange_CombinesAllVisibleSeriesAndPadsByRangePadding() {
        var cut = RenderChart(
            XAxisKind.Int,
            SeriesFragment<NTLineSeries<Pt>>([new Pt(0, 1m), new Pt(10, 2m)]),
            SeriesFragment<NTLineSeries<Pt>>([new Pt(5, 1m), new Pt(20, 2m)]));

        var unpadded = ChartXRange(cut.Instance);
        var padded = ChartXRange(cut.Instance, true);

        unpadded.Should().Be((0d, 20d));
        padded.Min.Should().BeApproximately(-1d, 1e-9);
        padded.Max.Should().BeApproximately(21d, 1e-9);
    }

    [Fact]
    public void ChartRange_ExcludesHiddenSeries() {
        var cut = RenderChart(
            XAxisKind.Int,
            SeriesFragment<NTLineSeries<Pt>>([new Pt(0, 1m), new Pt(10, 2m)]),
            SeriesFragment<NTLineSeries<Pt>>([new Pt(5, 1m), new Pt(500, 2m)], ("Visible", false)));

        ChartXRange(cut.Instance).Should().Be((0d, 10d));
    }

    [Fact]
    public void ChartRange_WithNoSeriesData_FallsBackToUnitRange() {
        var cut = RenderChart(XAxisKind.Int, SeriesFragment<NTLineSeries<Pt>>([]));

        ChartXRange(cut.Instance, true).Should().Be((0d, 1d));
    }

    [Fact]
    public void ChartRange_WithViewRange_UsesViewRangeWithoutPadding() {
        var cut = RenderChart(XAxisKind.Int, SeriesFragment<NTLineSeries<Pt>>(Linear(10)));

        OnDispatcher(cut, () => cut.Instance.SetViewXRange(3, 6));

        ChartXRange(cut.Instance, true).Should().Be((3d, 6d));
    }
}
