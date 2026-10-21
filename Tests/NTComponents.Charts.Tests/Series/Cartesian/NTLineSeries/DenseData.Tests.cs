using NTComponents.Charts.Core.Series;

namespace NTComponents.Charts.Tests.Series.Cartesian;

/// <summary>Lines with many more points than plot pixels, where only the stroked path is reduced to per-column extremes.</summary>
public class NTLineSeriesDenseData_Tests : CartesianChartTestBase {
    private static readonly ChartSetup _setup = new() { XAxis = XAxisKind.Int };

    // About seven points per pixel column, alternating between two levels so decimation drops interior points.
    private static Pt[] Zigzag(int count) => [.. Enumerable.Range(1, count).Select(i => new Pt(i, i % 2 == 0 ? 10m : 20m + (i % 7)))];

    [Fact]
    public void HitTestOnTheStroke_ResolvesToARealDataPoint() {
        var data = Zigzag(2000);
        var cut = RenderChart(XAxisKind.Int, SeriesFragment<NTLineSeries<Pt>>(data, ("PointStyle", PointStyle.None)));
        var series = SeriesOf<NTLineSeries<Pt>>(cut);
        using var _ = Draw(cut, series);

        var hit = series.HitTest(ExpectedPoint(cut.Instance, 1001, data[1000].Y), Area);

        hit.Should().NotBeNull();
        hit!.Value.Data.Should().BeSameAs(data[hit.Value.Index]);
    }

    [Fact]
    public void SwitchingFromSolidToDashed_DrawsTheSameImageAsAFreshDashedLine() {
        var data = Zigzag(2000);
        var switchedChart = RenderChart(_setup, SeriesFragment<NTLineSeries<Pt>>(data, ("PointStyle", PointStyle.None)));
        using (Draw(switchedChart, SeriesOf<NTLineSeries<Pt>>(switchedChart))) { }
        ReRender(switchedChart, _setup, SeriesFragment<NTLineSeries<Pt>>(data, ("PointStyle", PointStyle.None), ("LineStyle", LineStyle.Dashed)));
        var freshChart = RenderChart(_setup, SeriesFragment<NTLineSeries<Pt>>(data, ("PointStyle", PointStyle.None), ("LineStyle", LineStyle.Dashed)));

        using var switched = Draw(switchedChart, SeriesOf<NTLineSeries<Pt>>(switchedChart));
        using var fresh = Draw(freshChart, SeriesOf<NTLineSeries<Pt>>(freshChart));

        Snapshot(switched).Should().Equal(Snapshot(fresh));
    }
}
