using NTComponents.Charts.Core;
using NTComponents.Charts.Core.Series;
using SkiaSharp;

namespace NTComponents.Charts.Tests.Series.Cartesian;

public class NTLineSeriesHitTest_Tests : CartesianChartTestBase {
    private static SKPoint Offset(SKPoint point, float dx, float dy) => new(point.X + dx, point.Y + dy);

    [Fact]
    public void WithEmptyData_ReturnsNull() {
        var cut = RenderChart(XAxisKind.Int, SeriesFragment<NTLineSeries<Pt>>([]));

        SeriesOf<NTLineSeries<Pt>>(cut).HitTest(new SKPoint(100, 100), Area).Should().BeNull();
    }

    [Fact]
    public void OnADataPoint_ReturnsItsIndexAndData() {
        var data = Linear(3);
        var cut = RenderChart(XAxisKind.Int, SeriesFragment<NTLineSeries<Pt>>(data));
        var series = SeriesOf<NTLineSeries<Pt>>(cut);

        for (var i = 0; i < data.Length; i++) {
            var hit = series.HitTest(ExpectedPoint(cut.Instance, i + 1, data[i].Y), Area);

            hit.Should().NotBeNull();
            hit!.Value.Index.Should().Be(i);
            hit.Value.Data.Should().BeSameAs(data[i]);
        }
    }

    [Fact]
    public void WithUnsortedData_ReturnsTheOriginalIndexOfTheHitPoint() {
        var data = new[] { new Pt(3, 30m), new Pt(1, 10m), new Pt(2, 20m) };
        var cut = RenderChart(XAxisKind.Int, SeriesFragment<NTLineSeries<Pt>>(data));
        var series = SeriesOf<NTLineSeries<Pt>>(cut);

        var hit = series.HitTest(ExpectedPoint(cut.Instance, 1, 10m), Area);

        hit!.Value.Index.Should().Be(1);
        hit.Value.Data.Should().BeSameAs(data[1]);
    }

    [Fact]
    public void FarFromEveryPointAndLine_ReturnsNull() {
        var cut = RenderChart(XAxisKind.Int, SeriesFragment<NTLineSeries<Pt>>(Linear(3)));

        SeriesOf<NTLineSeries<Pt>>(cut).HitTest(new SKPoint(150, 5), Area).Should().BeNull();
    }

    [Fact]
    public void PointRadius_IsPointSizePlusFivePixels() {
        var cut = RenderChart(XAxisKind.Int, SeriesFragment<NTLineSeries<Pt>>(Linear(3), ("LineStyle", LineStyle.None)));
        var series = SeriesOf<NTLineSeries<Pt>>(cut);
        var point = ExpectedPoint(cut.Instance, 2, 20m);

        series.HitTest(Offset(point, 12.5f, 0), Area)!.Value.Index.Should().Be(1);
        series.HitTest(Offset(point, 0, -12.5f), Area)!.Value.Index.Should().Be(1);
        series.HitTest(Offset(point, 13.5f, 0), Area).Should().BeNull();
        series.HitTest(Offset(point, 0, 13.5f), Area).Should().BeNull();
    }

    [Fact]
    public void LargerPointSize_WidensTheHitRadius() {
        var cut = RenderChart(XAxisKind.Int, SeriesFragment<NTLineSeries<Pt>>(Linear(3), ("LineStyle", LineStyle.None), ("PointSize", 20f)));
        var series = SeriesOf<NTLineSeries<Pt>>(cut);
        var point = ExpectedPoint(cut.Instance, 2, 20m);

        series.HitTest(Offset(point, 24f, 0), Area)!.Value.Index.Should().Be(1);
        series.HitTest(Offset(point, 26f, 0), Area).Should().BeNull();
    }

    [Fact]
    public void OnTheLineBetweenPoints_ReturnsTheNearestPoint() {
        var data = new[] { new Pt(1, 10m), new Pt(2, 10m), new Pt(3, 10m) };
        var cut = RenderChart(XAxisKind.Int, SeriesFragment<NTLineSeries<Pt>>(data));
        var series = SeriesOf<NTLineSeries<Pt>>(cut);
        var p0 = ExpectedPoint(cut.Instance, 1, 10m);
        var p1 = ExpectedPoint(cut.Instance, 2, 10m);

        var nearFirst = series.HitTest(new SKPoint(p0.X + ((p1.X - p0.X) * 0.3f), p0.Y), Area);
        var nearSecond = series.HitTest(new SKPoint(p0.X + ((p1.X - p0.X) * 0.7f), p0.Y), Area);

        nearFirst!.Value.Index.Should().Be(0);
        nearSecond!.Value.Index.Should().Be(1);
    }

    [Fact]
    public void NextToTheLine_HitsWithinHalfOfStrokeWidthPlusFiveAndMissesBeyond() {
        var data = new[] { new Pt(1, 10m), new Pt(2, 10m) };
        var cut = RenderChart(XAxisKind.Int, SeriesFragment<NTLineSeries<Pt>>(data));
        var series = SeriesOf<NTLineSeries<Pt>>(cut);
        var p0 = ExpectedPoint(cut.Instance, 1, 10m);
        var p1 = ExpectedPoint(cut.Instance, 2, 10m);
        var x = p0.X + ((p1.X - p0.X) * 0.4f);

        // The hit-test stroke is StrokeWidth (2) + 10 = 12px wide, i.e. 6px each side of the line.
        series.HitTest(new SKPoint(x, p0.Y + 5f), Area).Should().NotBeNull();
        series.HitTest(new SKPoint(x, p0.Y - 5f), Area).Should().NotBeNull();
        series.HitTest(new SKPoint(x, p0.Y + 8f), Area).Should().BeNull();
        series.HitTest(new SKPoint(x, p0.Y - 8f), Area).Should().BeNull();
    }

    [Fact]
    public void ChangingStrokeWidth_ChangesTheLineHitCorridor() {
        var data = new[] { new Pt(1, 10m), new Pt(2, 10m) };
        var cut = RenderChart(XAxisKind.Int, SeriesFragment<NTLineSeries<Pt>>(data));
        var series = SeriesOf<NTLineSeries<Pt>>(cut);
        var p0 = ExpectedPoint(cut.Instance, 1, 10m);
        var p1 = ExpectedPoint(cut.Instance, 2, 10m);
        var probe = new SKPoint(p0.X + ((p1.X - p0.X) * 0.4f), p0.Y + 10f);
        series.HitTest(probe, Area).Should().BeNull();

        series.StrokeWidth = 14f;

        series.HitTest(probe, Area).Should().NotBeNull();
    }

    [Fact]
    public void WithLineStyleNone_BetweenPointsIsNotAHit() {
        var cut = RenderChart(XAxisKind.Int, SeriesFragment<NTLineSeries<Pt>>([new Pt(1, 10m), new Pt(2, 10m)], ("LineStyle", LineStyle.None)));
        var p0 = ExpectedPoint(cut.Instance, 1, 10m);
        var p1 = ExpectedPoint(cut.Instance, 2, 10m);

        SeriesOf<NTLineSeries<Pt>>(cut).HitTest(new SKPoint((p0.X + p1.X) / 2, p0.Y), Area).Should().BeNull();
    }

    [Fact]
    public void WithSinglePoint_HitsOnlyAroundThatPoint() {
        var cut = RenderChart(XAxisKind.Int, SeriesFragment<NTLineSeries<Pt>>([new Pt(5, 50m)]));
        var series = SeriesOf<NTLineSeries<Pt>>(cut);
        var point = ExpectedPoint(cut.Instance, 5, 50m);

        series.HitTest(point, Area)!.Value.Index.Should().Be(0);
        series.HitTest(Offset(point, 40, 0), Area).Should().BeNull();
    }

    [Fact]
    public void OverlappingPoints_ReturnTheFirstPointInSeriesOrder() {
        var data = new[] { new Pt(1, 10m), new Pt(1.01, 10m), new Pt(100, 50m) };
        var cut = RenderChart(XAxisKind.Double, SeriesFragment<NTLineSeries<Pt>>(data, ("LineStyle", LineStyle.None)));

        var hit = SeriesOf<NTLineSeries<Pt>>(cut).HitTest(ExpectedPoint(cut.Instance, 1.01, 10m), Area);

        hit!.Value.Index.Should().Be(0);
    }

    [Fact]
    public void AfterReplacingData_UsesTheNewPointPositions() {
        var cut = RenderChart(XAxisKind.Int, SeriesFragment<NTLineSeries<Pt>>(Linear(3), ("LineStyle", LineStyle.None)));
        var series = SeriesOf<NTLineSeries<Pt>>(cut);
        var oldPosition = ExpectedPoint(cut.Instance, 2, 20m);
        series.HitTest(oldPosition, Area).Should().NotBeNull();

        ReRender(cut, new ChartSetup(), SeriesFragment<NTLineSeries<Pt>>([new Pt(1, 10m), new Pt(2, 90m), new Pt(3, 30m)], ("LineStyle", LineStyle.None)));

        series.HitTest(oldPosition, Area).Should().BeNull();
        series.HitTest(ExpectedPoint(cut.Instance, 2, 90m), Area)!.Value.Index.Should().Be(1);
    }

    [Fact]
    public void AfterInPlaceMutationAndInvalidate_UsesTheNewPointPositions() {
        var data = Linear(3).ToList();
        var cut = RenderChart(XAxisKind.Int, SeriesFragment<NTLineSeries<Pt>>(data, ("LineStyle", LineStyle.None)));
        var series = SeriesOf<NTLineSeries<Pt>>(cut);
        series.HitTest(ExpectedPoint(cut.Instance, 3, 30m), Area).Should().NotBeNull();

        data[2] = data[2] with { Y = 300m };
        series.Invalidate();

        series.HitTest(ExpectedPoint(cut.Instance, 3, 300m), Area)!.Value.Index.Should().Be(2);
    }

    [Fact]
    public void WithSecondaryYAxis_HitsPointsAtSecondaryScaledPositions() {
        var cut = RenderChart(
            new ChartSetup { SecondaryYAxis = true },
            SeriesFragment<NTLineSeries<Pt>>(Linear(3), ("LineStyle", LineStyle.None)),
            SeriesFragment<NTLineSeries<Pt>>(Linear(3, i => 1000m * (i + 1)), ("LineStyle", LineStyle.None), ("UseSecondaryYAxis", true)));
        var secondary = SeriesOf<NTLineSeries<Pt>>(cut, 1);

        var hit = secondary.HitTest(ExpectedPoint(cut.Instance, 2, 2000m, secondary: true), Area);

        hit!.Value.Index.Should().Be(1);
        hit.Value.Data!.Y.Should().Be(2000m);
    }

    [Fact]
    public void WithDateTimeX_HitsPointsAtTheirTimePosition() {
        var data = new[] { new Pt(new DateTime(2024, 1, 1), 10m), new Pt(new DateTime(2024, 1, 2), 30m), new Pt(new DateTime(2024, 1, 3), 20m) };
        var cut = RenderChart(XAxisKind.DateTime, SeriesFragment<NTLineSeries<Pt>>(data, ("LineStyle", LineStyle.None)));
        var series = SeriesOf<NTLineSeries<Pt>>(cut);

        var hit = series.HitTest(ExpectedPoint(cut.Instance, ((DateTime)data[2].X).Ticks, 20m), Area);

        hit!.Value.Index.Should().Be(2);
    }

    [Fact]
    public void WithCategoricalX_HitsPointsAtTheirCategoryPosition() {
        var data = new[] { new Pt("A", 10m), new Pt("B", 30m), new Pt("C", 20m) };
        var cut = RenderChart(XAxisKind.Default, SeriesFragment<NTLineSeries<Pt>>(data, ("LineStyle", LineStyle.None)));
        var series = SeriesOf<NTLineSeries<Pt>>(cut);

        var hit = series.HitTest(ExpectedPoint(cut.Instance, 1, 30m), Area);

        hit!.Value.Index.Should().Be(1);
        hit.Value.Data!.X.Should().Be("B");
    }
}
