using NTComponents.Charts.Core;
using NTComponents.Charts.Core.Series;
using SkiaSharp;

namespace NTComponents.Charts.Tests.Series.Cartesian;

public class NTDataPointRenderArgs_Tests : CartesianChartTestBase {
    private static (string, object) OnRender(Action<NTDataPointRenderArgs<Pt>> callback) => ("OnDataPointRender", callback);

    [Fact]
    public void Line_ReceivesEveryPointWithItsOriginalIndexDataAndSeriesDefaults() {
        var data = new[] { new Pt(3, 30m), new Pt(1, 10m), new Pt(2, 20m) };
        var received = new List<NTDataPointRenderArgs<Pt>>();
        var snapshots = new List<(Pt Data, int Index, SKColor? Color, float? Size)>();
        var cut = RenderChart(XAxisKind.Int, SeriesFragment<NTLineSeries<Pt>>(data, ("Color", NTColor.Primary), ("PointSize", 11f), OnRender(args => {
            received.Add(args);
            snapshots.Add((args.Data, args.Index, args.Color, args.PointSize));
        })));

        using var surface = Draw(cut, SeriesOf<NTLineSeries<Pt>>(cut));

        // Points are visited in X order, but Index refers to the position in the bound data.
        var markerCalls = snapshots.TakeLast(3).ToList();
        markerCalls.Select(s => s.Index).Should().Equal(1, 2, 0);
        markerCalls.Select(s => s.Data).Should().Equal(data[1], data[2], data[0]);
        markerCalls.Should().OnlyContain(s => s.Color == Red && s.Size == 11f);
        received.Should().OnlyContain(a => a.GetThemeColor(NTColor.Primary) == Red && a.GetThemeColor(NTColor.Secondary) == Blue);
    }

    [Fact]
    public void Line_ColorOverrideRecolorsOnlyThatMarker() {
        var cut = RenderChart(XAxisKind.Int, SeriesFragment<NTLineSeries<Pt>>(Linear(3), ("Color", NTColor.Primary), ("LineStyle", LineStyle.None), OnRender(args => {
            if (args.Index == 1) {
                args.Color = SKColors.Lime;
            }
        })));

        using var surface = Draw(cut, SeriesOf<NTLineSeries<Pt>>(cut));

        IsClose(PixelAt(surface, ExpectedPoint(cut.Instance, 1, 10m)), Red).Should().BeTrue();
        IsClose(PixelAt(surface, ExpectedPoint(cut.Instance, 2, 20m)), Green).Should().BeTrue();
        IsClose(PixelAt(surface, ExpectedPoint(cut.Instance, 3, 30m)), Red).Should().BeTrue();
    }

    [Fact]
    public void Line_PointSizeOverrideResizesOnlyThatMarker() {
        var cut = RenderChart(XAxisKind.Int, SeriesFragment<NTLineSeries<Pt>>(Linear(3), ("Color", NTColor.Primary), ("LineStyle", LineStyle.None), OnRender(args => {
            if (args.Index == 1) {
                args.PointSize = 30f;
            }
        })));
        var big = ExpectedPoint(cut.Instance, 2, 20m);
        var small = ExpectedPoint(cut.Instance, 3, 30m);

        using var surface = Draw(cut, SeriesOf<NTLineSeries<Pt>>(cut));

        IsClose(PixelAt(surface, new SKPoint(big.X + 12f, big.Y)), Red).Should().BeTrue();
        IsWhite(PixelAt(surface, new SKPoint(small.X + 12f, small.Y))).Should().BeTrue();
    }

    [Fact]
    public void Line_PointShapeOverrideChangesTheMarkerShape() {
        var cut = RenderChart(XAxisKind.Int, SeriesFragment<NTLineSeries<Pt>>(Linear(2), ("Color", NTColor.Primary), ("LineStyle", LineStyle.None), ("PointSize", 20f), OnRender(args => args.PointShape = args.Index == 1 ? PointShape.Square : PointShape.Circle)));
        var circle = ExpectedPoint(cut.Instance, 1, 10m);
        var square = ExpectedPoint(cut.Instance, 2, 20m);

        using var surface = Draw(cut, SeriesOf<NTLineSeries<Pt>>(cut));

        IsWhite(PixelAt(surface, new SKPoint(circle.X + 8.5f, circle.Y + 8.5f))).Should().BeTrue();
        IsClose(PixelAt(surface, new SKPoint(square.X + 8.5f, square.Y + 8.5f)), Red).Should().BeTrue();
    }

    [Fact]
    public void Line_SegmentCallbackIsKeyedByTheSegmentEndPoint() {
        var cut = RenderChart(XAxisKind.Int, SeriesFragment<NTLineSeries<Pt>>(Linear(3), ("Color", NTColor.Primary), ("StrokeWidth", 6f), ("PointStyle", PointStyle.None), OnRender(args => {
            if (args.Index == 2) {
                args.Color = SKColors.Blue;
            }
        })));
        var p0 = ExpectedPoint(cut.Instance, 1, 10m);
        var p1 = ExpectedPoint(cut.Instance, 2, 20m);
        var p2 = ExpectedPoint(cut.Instance, 3, 30m);

        using var surface = Draw(cut, SeriesOf<NTLineSeries<Pt>>(cut));

        IsClose(PixelAt(surface, new SKPoint((p0.X + p1.X) / 2, (p0.Y + p1.Y) / 2)), Red).Should().BeTrue("segment ending at index 1 keeps the series color");
        IsClose(PixelAt(surface, new SKPoint((p1.X + p2.X) / 2, (p1.Y + p2.Y) / 2)), Blue).Should().BeTrue("segment ending at index 2 is recolored");
    }

    [Fact]
    public void Line_SegmentLineStyleNoneHidesJustThatSegment() {
        var cut = RenderChart(XAxisKind.Int, SeriesFragment<NTLineSeries<Pt>>(Linear(3), ("Color", NTColor.Primary), ("StrokeWidth", 6f), ("PointStyle", PointStyle.None), OnRender(args => {
            if (args.Index == 2) {
                args.LineStyle = LineStyle.None;
            }
        })));
        var p0 = ExpectedPoint(cut.Instance, 1, 10m);
        var p1 = ExpectedPoint(cut.Instance, 2, 20m);
        var p2 = ExpectedPoint(cut.Instance, 3, 30m);

        using var surface = Draw(cut, SeriesOf<NTLineSeries<Pt>>(cut));

        IsClose(PixelAt(surface, new SKPoint((p0.X + p1.X) / 2, (p0.Y + p1.Y) / 2)), Red).Should().BeTrue();
        IsWhite(PixelAt(surface, new SKPoint((p1.X + p2.X) / 2, (p1.Y + p2.Y) / 2))).Should().BeTrue();
    }

    [Fact]
    public void Line_SegmentStrokeWidthOverrideThickensJustThatSegment() {
        var cut = RenderChart(XAxisKind.Int, SeriesFragment<NTLineSeries<Pt>>(Linear(3, _ => 50m), ("Color", NTColor.Primary), ("PointStyle", PointStyle.None), OnRender(args => {
            if (args.Index == 2) {
                args.StrokeWidth = 12f;
            }
        })));
        var p0 = ExpectedPoint(cut.Instance, 1, 50m);
        var p1 = ExpectedPoint(cut.Instance, 2, 50m);
        var p2 = ExpectedPoint(cut.Instance, 3, 50m);

        using var surface = Draw(cut, SeriesOf<NTLineSeries<Pt>>(cut));

        ColumnCount(surface, (int)((p0.X + p1.X) / 2), Red).Should().BeLessThan(4);
        ColumnCount(surface, (int)((p1.X + p2.X) / 2), Red).Should().BeGreaterThan(9);
    }

    [Fact]
    public void Bar_ReceivesIndexAndDataInDataOrderAndCanRecolor() {
        var data = new[] { new Pt("A", 10m), new Pt("B", 20m) };
        var seen = new List<(int Index, Pt Data)>();
        var cut = RenderChart(XAxisKind.Default, SeriesFragment<NTBarSeries<Pt>>(data, ("Color", NTColor.Primary), OnRender(args => {
            seen.Add((args.Index, args.Data));
            if (args.Index == 1) {
                args.Color = SKColors.Blue;
            }
        })));

        using var surface = Draw(cut, SeriesOf<NTBarSeries<Pt>>(cut));

        seen.Should().Equal([(0, data[0]), (1, data[1])]);
        var series = SeriesOf<NTBarSeries<Pt>>(cut);
        var first = series.HitTest(new SKPoint(0, 0), Area);
        first.Should().BeNull();
        ColumnCount(surface, 75, Red).Should().BeGreaterThan(10);
        ColumnCount(surface, 225, Blue).Should().BeGreaterThan(10);
    }

    [Fact]
    public void ResetsToSeriesDefaults_OnEveryPaint() {
        var calls = 0;
        var cut = RenderChart(XAxisKind.Int, SeriesFragment<NTLineSeries<Pt>>(Linear(2), OnRender(args => {
            calls++;
            args.Color.Should().NotBeNull("each callback starts from the series color");
        })));
        var series = SeriesOf<NTLineSeries<Pt>>(cut);

        using (Draw(cut, series)) {
        }

        var afterFirst = calls;
        using (Draw(cut, series)) {
        }

        afterFirst.Should().BeGreaterThan(0);
        calls.Should().Be(afterFirst * 2);
    }
}
