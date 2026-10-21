using NTComponents.Charts.Core;
using NTComponents.Charts.Core.Series;
using SkiaSharp;

namespace NTComponents.Charts.Tests.Series.Cartesian;

public class NTAreaSeries_Tests : CartesianChartTestBase {
    [Fact]
    public void NewInstance_HasDocumentedDefaults() {
        var series = new NTAreaSeries<Pt>();

        series.AreaOpacity.Should().Be(0.3f);
        series.BaselineValue.Should().Be(0m);
        series.LineStyle.Should().Be(LineStyle.Solid);
    }

    [Fact]
    public void Render_DrawsTheLineLikeALineSeries() {
        var data = Linear(4);
        var area = RenderChart(XAxisKind.Int, SeriesFragment<NTAreaSeries<Pt>>(data, ("Color", NTColor.Primary), ("StrokeWidth", 6f), ("PointStyle", PointStyle.None)));
        var p0 = ExpectedPoint(area.Instance, 1, 10m);
        var p1 = ExpectedPoint(area.Instance, 2, 20m);

        using var surface = Draw(area, SeriesOf<NTAreaSeries<Pt>>(area));

        IsClose(PixelAt(surface, new SKPoint((p0.X + p1.X) / 2, (p0.Y + p1.Y) / 2)), Red).Should().BeTrue();
    }

    [Fact]
    public void HitTest_HitsDataPointsAndReturnsNullAwayFromThem() {
        var cut = RenderChart(XAxisKind.Int, SeriesFragment<NTAreaSeries<Pt>>(Linear(3)));
        var series = SeriesOf<NTAreaSeries<Pt>>(cut);

        series.HitTest(ExpectedPoint(cut.Instance, 2, 20m), Area)!.Value.Index.Should().Be(1);
        series.HitTest(new SKPoint(150, 2), Area).Should().BeNull();
    }

    [Fact]
    public void Ranges_MatchTheDataExtents() {
        var cut = RenderChart(XAxisKind.Int, SeriesFragment<NTAreaSeries<Pt>>([new Pt(2, -4m), new Pt(8, 12m)]));
        var series = SeriesOf<NTAreaSeries<Pt>>(cut);

        series.GetXRange().Should().Be((2d, 8d));
        series.GetYRange().Should().Be((-4m, 12m));
    }

    [Fact]
    public void Render_FillsTheRegionBetweenTheLineAndTheBaseline() {
        var cut = RenderChart(XAxisKind.Int, SeriesFragment<NTAreaSeries<Pt>>([new Pt(1, 50m), new Pt(2, 50m), new Pt(3, 50m)], ("Color", NTColor.Primary), ("AreaOpacity", 0.5f), ("PointStyle", PointStyle.None)));
        var line = ExpectedPoint(cut.Instance, 2, 50m);

        using var surface = Draw(cut, SeriesOf<NTAreaSeries<Pt>>(cut));

        var below = PixelAt(surface, new SKPoint(line.X, line.Y + 30));
        IsWhite(below).Should().BeFalse("the area under the line is tinted with the series color");
        below.Red.Should().BeGreaterThan(below.Green);
    }
}
