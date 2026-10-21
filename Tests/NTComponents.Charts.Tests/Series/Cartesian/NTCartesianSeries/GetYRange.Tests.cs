namespace NTComponents.Charts.Tests.Series.Cartesian;

public class GetYRange_Tests : CartesianChartTestBase {
    [Fact]
    public void WithEmptyData_ReturnsNull() {
        var cut = RenderChart(XAxisKind.Int, SeriesFragment<NTLineSeries<Pt>>([]));

        SeriesOf<NTLineSeries<Pt>>(cut).GetYRange().Should().BeNull();
    }

    [Fact]
    public void WithSinglePoint_ReturnsCollapsedRange() {
        var cut = RenderChart(XAxisKind.Int, SeriesFragment<NTLineSeries<Pt>>([new Pt(1, 42m)]));

        SeriesOf<NTLineSeries<Pt>>(cut).GetYRange().Should().Be((42m, 42m));
    }

    [Fact]
    public void WithNegativeAndPositiveValues_ReturnsExtremes() {
        var cut = RenderChart(XAxisKind.Int, SeriesFragment<NTLineSeries<Pt>>([new Pt(1, -5m), new Pt(2, 12m), new Pt(3, 0m), new Pt(4, -20m)]));

        SeriesOf<NTLineSeries<Pt>>(cut).GetYRange().Should().Be((-20m, 12m));
    }

    [Fact]
    public void WithOnlyNegativeValues_ReturnsNegativeExtremes() {
        var cut = RenderChart(XAxisKind.Int, SeriesFragment<NTLineSeries<Pt>>([new Pt(1, -5m), new Pt(2, -9m)]));

        SeriesOf<NTLineSeries<Pt>>(cut).GetYRange().Should().Be((-9m, -5m));
    }

    [Fact]
    public void WithFractionalDecimals_PreservesDecimalPrecision() {
        var cut = RenderChart(XAxisKind.Int, SeriesFragment<NTLineSeries<Pt>>([new Pt(1, 0.0000001m), new Pt(2, 123456789.123456789m)]));

        SeriesOf<NTLineSeries<Pt>>(cut).GetYRange().Should().Be((0.0000001m, 123456789.123456789m));
    }

    [Fact]
    public void WithUnsortedX_ReturnsExtremesRegardlessOfInputOrder() {
        var cut = RenderChart(XAxisKind.Int, SeriesFragment<NTLineSeries<Pt>>([new Pt(9, 90m), new Pt(1, 10m), new Pt(5, 50m)]));

        SeriesOf<NTLineSeries<Pt>>(cut).GetYRange().Should().Be((10m, 90m));
    }

    [Fact]
    public void WithWindow_IncludesPointsInsideWindowPlusOneNeighbourOnEachSide() {
        var cut = RenderChart(XAxisKind.Int, SeriesFragment<NTLineSeries<Pt>>(Linear(10)));

        var range = SeriesOf<NTLineSeries<Pt>>(cut).GetYRange(4, 6);

        range.Should().Be((30m, 70m));
    }

    [Fact]
    public void WithWindowAtDataStart_ClampsOverscanToFirstPoint() {
        var cut = RenderChart(XAxisKind.Int, SeriesFragment<NTLineSeries<Pt>>(Linear(10)));

        SeriesOf<NTLineSeries<Pt>>(cut).GetYRange(0, 2).Should().Be((10m, 30m));
    }

    [Fact]
    public void WithWindowAtDataEnd_IncludesOneLeftNeighbourAndClampsRightOverscan() {
        var cut = RenderChart(XAxisKind.Int, SeriesFragment<NTLineSeries<Pt>>(Linear(10)));

        SeriesOf<NTLineSeries<Pt>>(cut).GetYRange(9, 20).Should().Be((80m, 100m));
    }

    [Fact]
    public void WithReversedWindowBounds_ReturnsSameAsForwardWindow() {
        var cut = RenderChart(XAxisKind.Int, SeriesFragment<NTLineSeries<Pt>>(Linear(10)));
        var series = SeriesOf<NTLineSeries<Pt>>(cut);

        series.GetYRange(6, 4).Should().Be(series.GetYRange(4, 6));
    }

    [Fact]
    public void WithWindowAfterAllData_ReturnsNull() {
        var cut = RenderChart(XAxisKind.Int, SeriesFragment<NTLineSeries<Pt>>(Linear(10)));

        SeriesOf<NTLineSeries<Pt>>(cut).GetYRange(50, 60).Should().BeNull();
    }

    [Fact]
    public void WithWindowBeforeAllData_ReturnsNull() {
        var cut = RenderChart(XAxisKind.Int, SeriesFragment<NTLineSeries<Pt>>(Linear(10)));

        SeriesOf<NTLineSeries<Pt>>(cut).GetYRange(-60, -50).Should().BeNull();
    }

    [Fact]
    public void WithOnlyOneWindowBound_ReturnsFullRange() {
        var cut = RenderChart(XAxisKind.Int, SeriesFragment<NTLineSeries<Pt>>(Linear(10)));
        var series = SeriesOf<NTLineSeries<Pt>>(cut);

        series.GetYRange(xMin: 4).Should().Be((10m, 100m));
        series.GetYRange(xMax: 4).Should().Be((10m, 100m));
    }

    [Fact]
    public void WithDifferentWindows_DoesNotReuseTheCachedWindowResult() {
        var cut = RenderChart(XAxisKind.Int, SeriesFragment<NTLineSeries<Pt>>(Linear(10)));
        var series = SeriesOf<NTLineSeries<Pt>>(cut);

        series.GetYRange(4, 6).Should().Be((30m, 70m));
        series.GetYRange(1, 2).Should().Be((10m, 30m));
        series.GetYRange(4, 6).Should().Be((30m, 70m));
    }

    [Fact]
    public void WithWindowAfterFullRange_FullRangeCacheDoesNotLeakIntoWindow() {
        var cut = RenderChart(XAxisKind.Int, SeriesFragment<NTLineSeries<Pt>>(Linear(10)));
        var series = SeriesOf<NTLineSeries<Pt>>(cut);

        series.GetYRange().Should().Be((10m, 100m));
        series.GetYRange(4, 6).Should().Be((30m, 70m));
        series.GetYRange().Should().Be((10m, 100m));
    }

    [Fact]
    public void AfterReplacingData_RecomputesFullAndWindowRanges() {
        var cut = RenderChart(XAxisKind.Int, SeriesFragment<NTLineSeries<Pt>>(Linear(10)));
        var series = SeriesOf<NTLineSeries<Pt>>(cut);
        series.GetYRange().Should().Be((10m, 100m));
        series.GetYRange(4, 6).Should().Be((30m, 70m));

        ReRender(cut, new ChartSetup(), SeriesFragment<NTLineSeries<Pt>>(Linear(10, i => (i + 1) * 100m)));

        series.GetYRange().Should().Be((100m, 1000m));
        series.GetYRange(4, 6).Should().Be((300m, 700m));
    }

    [Fact]
    public void AfterInPlaceMutationAndInvalidate_RecomputesRange() {
        var data = Linear(3).ToList();
        var cut = RenderChart(XAxisKind.Int, SeriesFragment<NTLineSeries<Pt>>(data));
        var series = SeriesOf<NTLineSeries<Pt>>(cut);
        series.GetYRange().Should().Be((10m, 30m));

        data[1] = data[1] with { Y = 500m };
        series.Invalidate();

        series.GetYRange().Should().Be((10m, 500m));
    }

    [Fact]
    public void WhenSeriesIsHidden_ScalesByVisibilityFactorToZero() {
        var cut = RenderChart(XAxisKind.Int, SeriesFragment<NTLineSeries<Pt>>(Linear(3), ("Visible", false)));

        SeriesOf<NTLineSeries<Pt>>(cut).GetYRange().Should().Be((0m, 0m));
    }

    [Fact]
    public void ChartRange_ExcludesHiddenSeries() {
        var cut = RenderChart(
            XAxisKind.Int,
            SeriesFragment<NTLineSeries<Pt>>(Linear(3)),
            SeriesFragment<NTLineSeries<Pt>>(Linear(3, i => 1000m * (i + 1)), ("Visible", false)));

        ChartYRange(cut.Instance).Should().Be((10m, 30m));
    }

    [Fact]
    public void ChartRange_AfterTogglingVisibilityOn_IncludesTheSeries() {
        var cut = RenderChart(
            XAxisKind.Int,
            SeriesFragment<NTLineSeries<Pt>>(Linear(3)),
            SeriesFragment<NTLineSeries<Pt>>(Linear(3, i => 1000m * (i + 1)), ("Visible", false)));

        ReRender(cut, new ChartSetup(), SeriesFragment<NTLineSeries<Pt>>(Linear(3)), SeriesFragment<NTLineSeries<Pt>>(Linear(3, i => 1000m * (i + 1)), ("Visible", true)));

        ChartYRange(cut.Instance).Should().Be((10m, 3000m));
    }

    [Fact]
    public void ChartRange_PrimaryAndSecondaryAxesOnlyIncludeTheirOwnSeries() {
        var setup = new ChartSetup { SecondaryYAxis = true };
        var cut = RenderChart(
            setup,
            SeriesFragment<NTLineSeries<Pt>>(Linear(3)),
            SeriesFragment<NTLineSeries<Pt>>(Linear(3, i => 1000m * (i + 1)), ("UseSecondaryYAxis", true)));

        ChartYRange(cut.Instance).Should().Be((10m, 30m));
        ChartYRange(cut.Instance, secondary: true).Should().Be((1000m, 3000m));
    }

    [Fact]
    public void ChartRange_PaddedRangeAddsRangePaddingOnBothSidesForLines() {
        var cut = RenderChart(XAxisKind.Int, SeriesFragment<NTLineSeries<Pt>>(Linear(3)));

        var (min, max) = ChartYRange(cut.Instance, padded: true);

        min.Should().Be(9m);
        max.Should().Be(31m);
    }

    [Fact]
    public void ChartRange_WithCustomRangePadding_ScalesThePadding() {
        var setup = new ChartSetup { ChartAttributes = [("RangePadding", 0.5)] };
        var cut = RenderChart(setup, SeriesFragment<NTLineSeries<Pt>>(Linear(3)));

        ChartYRange(cut.Instance, padded: true).Should().Be((0m, 40m));
    }

    [Fact]
    public void ChartRange_WithXViewRange_FitsYToTheVisibleWindow() {
        var cut = RenderChart(XAxisKind.Int, SeriesFragment<NTLineSeries<Pt>>(Linear(10)));

        OnDispatcher(cut, () => cut.Instance.SetViewXRange(4, 6));

        // Window (4..6) plus one neighbour point per side: x=3..7 -> y 30..70, padded by 5% of 40.
        ChartYRange(cut.Instance, padded: true).Should().Be((28m, 72m));
    }

    [Fact]
    public void ChartRange_WithNoData_FallsBackToUnitRange() {
        var cut = RenderChart(XAxisKind.Int, SeriesFragment<NTLineSeries<Pt>>([]));

        ChartYRange(cut.Instance, padded: true).Should().Be((0m, 1m));
    }

    [Fact]
    public void ChartRange_WithAbsoluteMinimumAbovePaddedMin_RaisesTheLowerBound() {
        var setup = new ChartSetup { YAxisAttributes = [("AbsoluteMinimum", (decimal?)20m)] };
        var cut = RenderChart(setup, SeriesFragment<NTLineSeries<Pt>>(Linear(3, i => (i + 1) * 10m)));

        ChartYRange(cut.Instance, padded: true).Should().Be((20m, 31m));
    }
}
