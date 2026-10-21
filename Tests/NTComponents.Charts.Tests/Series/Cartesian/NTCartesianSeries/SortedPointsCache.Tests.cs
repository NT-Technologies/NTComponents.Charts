using NTComponents.Charts.Core.Series;

namespace NTComponents.Charts.Tests.Series.Cartesian;

/// <summary>The sorted visible-point buffer is refilled in place on invalidation and must never keep stale points.</summary>
public class NTCartesianSeriesSortedPointsCache_Tests : CartesianChartTestBase {
    [Fact]
    public void ShrinkingDataInPlaceThenInvalidating_DropsTheRemovedPoints() {
        var data = new List<Pt> { new(1, 10m), new(2, 50m), new(3, 20m), new(4, 90m) };
        var cut = RenderChart(XAxisKind.Int, SeriesFragment<NTLineSeries<Pt>>(data));
        var series = SeriesOf<NTLineSeries<Pt>>(cut);
        series.GetXRange().Should().Be((1d, 4d));

        data.RemoveRange(2, 2);
        series.Invalidate();

        series.GetXRange().Should().Be((1d, 2d));
        series.GetYRange().Should().Be((10m, 50m));
    }
}
