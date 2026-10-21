namespace NTComponents.Charts.Tests.Series.Cartesian;

/// <summary>Categorical value registration is observable through the chart-wide distinct value lists.</summary>
public class RegisterValues_Tests : CartesianChartTestBase {
    [Fact]
    public void XValues_OfTwoSeries_AreDistinctInSeriesThenDataOrder() {
        var cut = RenderChart(
            XAxisKind.Default,
            SeriesFragment<NTLineSeries<Pt>>([new Pt("C", 1m), new Pt("A", 2m)]),
            SeriesFragment<NTLineSeries<Pt>>([new Pt("B", 1m), new Pt("A", 2m)]));

        cut.Instance.GetAllXValues().Should().Equal("C", "A", "B");
    }

    [Fact]
    public void XValues_SkipNullXValues() {
        var cut = RenderChart(XAxisKind.Default, SeriesFragment<NTLineSeries<Pt>>([new Pt("A", 1m), new Pt(null!, 2m), new Pt("B", 3m)]));

        cut.Instance.GetAllXValues().Should().Equal("A", "B");
    }

    [Fact]
    public void XValues_ExcludeHiddenSeries() {
        var cut = RenderChart(
            XAxisKind.Default,
            SeriesFragment<NTLineSeries<Pt>>([new Pt("A", 1m)]),
            SeriesFragment<NTLineSeries<Pt>>([new Pt("Z", 1m)], ("Visible", false)));

        cut.Instance.GetAllXValues().Should().Equal("A");
    }

    [Fact]
    public void XValues_AfterSeriesBecomesVisible_IncludeItsCategories() {
        var cut = RenderChart(
            XAxisKind.Default,
            SeriesFragment<NTLineSeries<Pt>>([new Pt("A", 1m)]),
            SeriesFragment<NTLineSeries<Pt>>([new Pt("Z", 1m)], ("Visible", false)));
        cut.Instance.GetAllXValues().Should().Equal("A");

        ReRender(cut, new ChartSetup { XAxis = XAxisKind.Default }, SeriesFragment<NTLineSeries<Pt>>([new Pt("A", 1m)]), SeriesFragment<NTLineSeries<Pt>>([new Pt("Z", 1m)], ("Visible", true)));

        cut.Instance.GetAllXValues().Should().Equal("A", "Z");
    }

    [Fact]
    public void XValues_AfterDataReplacement_ReflectTheNewData() {
        var cut = RenderChart(XAxisKind.Default, SeriesFragment<NTLineSeries<Pt>>([new Pt("A", 1m)]));
        cut.Instance.GetAllXValues().Should().Equal("A");

        ReRender(cut, new ChartSetup { XAxis = XAxisKind.Default }, SeriesFragment<NTLineSeries<Pt>>([new Pt("Q", 1m), new Pt("P", 1m)]));

        cut.Instance.GetAllXValues().Should().Equal("Q", "P");
    }

    [Fact]
    public void YValues_AreDistinctSortedYValuesOfVisibleSeries() {
        var cut = RenderChart(
            XAxisKind.Int,
            SeriesFragment<NTLineSeries<Pt>>([new Pt(1, 30m), new Pt(2, 10m), new Pt(3, 30m)]),
            SeriesFragment<NTLineSeries<Pt>>([new Pt(1, 20m)]),
            SeriesFragment<NTLineSeries<Pt>>([new Pt(1, 999m)], ("Visible", false)));

        cut.Instance.GetAllYValues().Should().Equal(10m, 20m, 30m);
    }

    [Fact]
    public void CategoricalX_MapsEachCategoryToItsFirstSeenIndex() {
        var cut = RenderChart(XAxisKind.Default, SeriesFragment<NTLineSeries<Pt>>([new Pt("C", 1m), new Pt("A", 2m), new Pt("B", 3m)]));

        cut.Instance.GetScaledXValue("C").Should().Be(0);
        cut.Instance.GetScaledXValue("A").Should().Be(1);
        cut.Instance.GetScaledXValue("B").Should().Be(2);
    }
}
