using NTComponents.Charts.Core;
using NTComponents.Charts.Core.Series;
using SkiaSharp;

namespace NTComponents.Charts.Tests.Series.Cartesian;

public class NTPointSeries_Tests : CartesianChartTestBase {
    [Fact]
    public void NewInstance_HasNoLine() {
        new NTPointSeries<Pt>().LineStyle.Should().Be(LineStyle.None);
    }

    [Fact]
    public void WithExplicitSolidLineStyle_StillDrawsNoLine() {
        var cut = RenderChart(XAxisKind.Int, SeriesFragment<NTPointSeries<Pt>>(Linear(3), ("Color", NTColor.Primary), ("LineStyle", LineStyle.Solid), ("StrokeWidth", 6f)));
        var series = SeriesOf<NTPointSeries<Pt>>(cut);
        var p0 = ExpectedPoint(cut.Instance, 1, 10m);
        var p1 = ExpectedPoint(cut.Instance, 2, 20m);

        using var surface = Draw(cut, series);

        IsClose(PixelAt(surface, p0), Red).Should().BeTrue("markers are drawn");
        IsWhite(PixelAt(surface, new SKPoint((p0.X + p1.X) / 2, (p0.Y + p1.Y) / 2))).Should().BeTrue("no connecting line");
    }

    [Fact]
    public void HitTest_OnlyHitsMarkersNotTheSpaceBetweenThem() {
        var cut = RenderChart(XAxisKind.Int, SeriesFragment<NTPointSeries<Pt>>(Linear(3)));
        var series = SeriesOf<NTPointSeries<Pt>>(cut);
        var p1 = ExpectedPoint(cut.Instance, 2, 20m);
        var p2 = ExpectedPoint(cut.Instance, 3, 30m);

        series.HitTest(p1, Area)!.Value.Index.Should().Be(1);
        series.HitTest(new SKPoint((p1.X + p2.X) / 2, (p1.Y + p2.Y) / 2), Area).Should().BeNull();
    }

    [Fact]
    public void DefaultMarkerShape_CyclesWithTheSeriesIndex() {
        var data = Linear(3);
        var cut = RenderChart(
            XAxisKind.Int,
            SeriesFragment<NTPointSeries<Pt>>(data, ("Color", NTColor.Primary), ("PointSize", 20f)),
            SeriesFragment<NTPointSeries<Pt>>(data.Select(p => p with { Y = p.Y + 100m }).ToArray(), ("Color", NTColor.Secondary), ("PointSize", 20f)));
        var circle = ExpectedPoint(cut.Instance, 2, 20m);
        var square = ExpectedPoint(cut.Instance, 2, 120m);

        using var surface = Draw(cut, SeriesOf<NTPointSeries<Pt>>(cut, 0), SeriesOf<NTPointSeries<Pt>>(cut, 1));

        // An 8px circle leaves the bounding-box corners empty, an 8px square fills them.
        IsWhite(PixelAt(surface, new SKPoint(circle.X + 8.5f, circle.Y + 8.5f))).Should().BeTrue();
        IsClose(PixelAt(surface, new SKPoint(square.X + 8.5f, square.Y + 8.5f)), Blue).Should().BeTrue();
    }

    [Theory]
    [InlineData(PointShape.Circle, false)]
    [InlineData(PointShape.Square, true)]
    [InlineData(PointShape.Triangle, false)]
    [InlineData(PointShape.Diamond, false)]
    public void ExplicitShape_FillsTheBoundingBoxCornerOnlyForSquares(PointShape shape, bool cornerFilled) {
        var cut = RenderChart(XAxisKind.Int, SeriesFragment<NTPointSeries<Pt>>([new Pt(5, 50m)], ("Color", NTColor.Primary), ("PointShape", shape), ("PointSize", 16f)));
        var center = ExpectedPoint(cut.Instance, 5, 50m);

        using var surface = Draw(cut, SeriesOf<NTPointSeries<Pt>>(cut));

        IsClose(PixelAt(surface, new SKPoint(center.X + 7f, center.Y - 7f)), Red).Should().Be(cornerFilled);
    }

    [Fact]
    public void TriangleShape_IsWideAtTheBaseAndNarrowAtTheApex() {
        var cut = RenderChart(XAxisKind.Int, SeriesFragment<NTPointSeries<Pt>>([new Pt(5, 50m)], ("Color", NTColor.Primary), ("PointShape", PointShape.Triangle), ("PointSize", 16f)));
        var center = ExpectedPoint(cut.Instance, 5, 50m);

        using var surface = Draw(cut, SeriesOf<NTPointSeries<Pt>>(cut));

        IsClose(PixelAt(surface, new SKPoint(center.X + 6f, center.Y + 6f)), Red).Should().BeTrue("base corner");
        IsWhite(PixelAt(surface, new SKPoint(center.X + 6f, center.Y - 6f))).Should().BeTrue("apex side is empty");
    }

    [Fact]
    public void LargerPointSize_PaintsFartherFromTheCenter() {
        byte[] Render(float size) {
            var cut = RenderChart(XAxisKind.Int, SeriesFragment<NTPointSeries<Pt>>([new Pt(5, 50m)], ("Color", NTColor.Primary), ("PointSize", size)));
            using var surface = Draw(cut, SeriesOf<NTPointSeries<Pt>>(cut));
            var center = ExpectedPoint(cut.Instance, 5, 50m);
            return [IsClose(PixelAt(surface, new SKPoint(center.X + 8f, center.Y)), Red) ? (byte)1 : (byte)0];
        }

        Render(8f)[0].Should().Be(0);
        Render(24f)[0].Should().Be(1);
    }

    [Fact]
    public void OutlinedStyle_LeavesTheMarkerCenterEmpty() {
        var cut = RenderChart(XAxisKind.Int, SeriesFragment<NTPointSeries<Pt>>([new Pt(5, 50m)], ("Color", NTColor.Primary), ("PointStyle", PointStyle.Outlined), ("PointSize", 24f)));
        var center = ExpectedPoint(cut.Instance, 5, 50m);

        using var surface = Draw(cut, SeriesOf<NTPointSeries<Pt>>(cut));

        IsWhite(PixelAt(surface, center)).Should().BeTrue();
        IsClose(PixelAt(surface, new SKPoint(center.X + 11f, center.Y)), Red, 60).Should().BeTrue();
    }

    [Fact]
    public void HoveredPoint_IsDrawnLarger() {
        var cut = RenderChart(XAxisKind.Int, SeriesFragment<NTPointSeries<Pt>>(Linear(3), ("Color", NTColor.Primary)));
        var series = SeriesOf<NTPointSeries<Pt>>(cut);
        var point = ExpectedPoint(cut.Instance, 2, 20m);
        var probe = new SKPoint(point.X + 4.5f, point.Y);
        using (var normal = Draw(cut, series)) {
            IsWhite(PixelAt(normal, probe)).Should().BeTrue();
        }

        SetHover(cut.Instance, series, 1);

        using var hovered = Draw(cut, series);
        IsClose(PixelAt(hovered, probe), Red).Should().BeTrue("hovered markers grow by 50%");
    }
}
