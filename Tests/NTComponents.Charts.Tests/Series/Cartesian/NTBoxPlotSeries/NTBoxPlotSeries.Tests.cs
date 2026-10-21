using NTComponents.Charts.Core;
using NTComponents.Charts.Core.Series;
using SkiaSharp;

namespace NTComponents.Charts.Tests.Series.Cartesian;

public class NTBoxPlotSeries_Tests : CartesianChartTestBase {
    private static readonly BoxPlotValues Values = new(10, 20, 30, 40, 50);

    private static (string, object) Selector(Func<Pt, BoxPlotValues> selector) => ("BoXValue", selector);

    private static Pt[] Three => [new Pt(1, 0m), new Pt(2, 0m), new Pt(3, 0m)];

    private IRenderedComponent<NTChart<Pt>> BoxPlot(Pt[] data, BoxPlotValues values, params (string, object)[] overrides) =>
        RenderChart(XAxisKind.Int, SeriesFragment<NTBoxPlotSeries<Pt>>(data, [Selector(_ => values), ("Color", NTColor.Primary), .. overrides]));

    /// <summary>Whether a pixel of <paramref name="color" /> exists within <paramref name="radius" /> pixels of the point.</summary>
    private static bool HasColorNear(SKSurface surface, SKPoint point, SKColor color, int radius = 2) {
        for (var dy = -radius; dy <= radius; dy++) {
            for (var dx = -radius; dx <= radius; dx++) {
                var x = (int)MathF.Floor(point.X) + dx;
                var y = (int)MathF.Floor(point.Y) + dy;
                if (x >= 0 && y >= 0 && x < SurfaceWidth && y < SurfaceHeight && IsClose(PixelAt(surface, x, y), color)) {
                    return true;
                }
            }
        }

        return false;
    }

    private static float Slot(IRenderedComponent<NTChart<Pt>> cut) => ExpectedPoint(cut.Instance, 3, 0m).X - ExpectedPoint(cut.Instance, 2, 0m).X;

    private static SKPoint At(IRenderedComponent<NTChart<Pt>> cut, double x, decimal y, float dx = 0) {
        var point = ExpectedPoint(cut.Instance, x, y);
        return new SKPoint(point.X + dx, point.Y);
    }

    [Fact]
    public void NewInstance_HasDocumentedDefaultRatios() {
        var series = new NTBoxPlotSeries<Pt>();

        series.BoxWidthRatio.Should().Be(0.6f);
        series.WhiskerWidthRatio.Should().Be(0.5f);
    }

    [Fact]
    public void YRange_SpansTheMinimumAndMaximumWhiskers() {
        var data = new[] { new Pt(1, 0m), new Pt(2, 0m) };
        var cut = RenderChart(XAxisKind.Int, SeriesFragment<NTBoxPlotSeries<Pt>>(data, Selector(p => p.X is 1 ? new BoxPlotValues(2, 4, 5, 6, 8) : new BoxPlotValues(10, 12, 14, 16, 20))));

        SeriesOf<NTBoxPlotSeries<Pt>>(cut).GetYRange().Should().Be((2m, 20m));
    }

    [Fact]
    public void YRange_IncludesOutliersBeyondTheWhiskers() {
        var data = new[] { new Pt(1, 0m), new Pt(2, 0m) };
        var cut = RenderChart(XAxisKind.Int, SeriesFragment<NTBoxPlotSeries<Pt>>(data, Selector(p => p.X is 1 ? new BoxPlotValues(2, 4, 5, 6, 8, [-3m, 1m]) : new BoxPlotValues(10, 12, 14, 16, 20, [35m]))));

        SeriesOf<NTBoxPlotSeries<Pt>>(cut).GetYRange().Should().Be((-3m, 35m));
    }

    [Fact]
    public void YRange_WithEmptyOutlierArray_UsesWhiskers() {
        var data = new[] { new Pt(1, 0m) };
        var cut = RenderChart(XAxisKind.Int, SeriesFragment<NTBoxPlotSeries<Pt>>(data, Selector(_ => new BoxPlotValues(2, 4, 5, 6, 8, []))));

        SeriesOf<NTBoxPlotSeries<Pt>>(cut).GetYRange().Should().Be((2m, 8m));
    }

    [Fact]
    public void YRange_ForAWindow_OnlyConsidersBoxesInsideItWithOneNeighbour() {
        var data = Enumerable.Range(1, 6).Select(i => new Pt(i, 0m)).ToArray();
        var cut = RenderChart(XAxisKind.Int, SeriesFragment<NTBoxPlotSeries<Pt>>(data, Selector(p => new BoxPlotValues(Convert.ToInt32(p.X) * 10, 0, 0, 0, Convert.ToInt32(p.X) * 10 + 5))));

        SeriesOf<NTBoxPlotSeries<Pt>>(cut).GetYRange(3, 4).Should().Be((20m, 55m));
    }

    [Fact]
    public void YRange_WithNoData_IsNull() {
        var cut = RenderChart(XAxisKind.Int, SeriesFragment<NTBoxPlotSeries<Pt>>([], Selector(_ => new BoxPlotValues(0, 0, 0, 0, 0))));

        SeriesOf<NTBoxPlotSeries<Pt>>(cut).GetYRange().Should().BeNull();
    }

    [Fact]
    public void XRange_IsWidenedByHalfABoxSlot() {
        var data = new[] { new Pt(3, 0m), new Pt(9, 0m) };
        var cut = RenderChart(XAxisKind.Int, SeriesFragment<NTBoxPlotSeries<Pt>>(data, Selector(_ => new BoxPlotValues(1, 2, 3, 4, 5))));

        SeriesOf<NTBoxPlotSeries<Pt>>(cut).GetXRange().Should().Be((0d, 12d));
    }

    [Fact]
    public void XRange_WithASingleBox_SpansOneSlot() {
        var cut = BoxPlot([new Pt(5, 0m)], Values);

        SeriesOf<NTBoxPlotSeries<Pt>>(cut).GetXRange().Should().Be((4.5d, 5.5d));
    }

    [Fact]
    public void ChartYRange_UsesBoxPlotExtentsAndExcludesHiddenSeries() {
        var data = new[] { new Pt(1, 0m) };
        var cut = RenderChart(
            XAxisKind.Int,
            SeriesFragment<NTBoxPlotSeries<Pt>>(data, Selector(_ => new BoxPlotValues(10, 12, 14, 16, 20))),
            SeriesFragment<NTBoxPlotSeries<Pt>>(data, Selector(_ => new BoxPlotValues(0, 0, 0, 0, 999)), ("Visible", false)));

        ChartYRange(cut.Instance).Should().Be((10m, 20m));
    }

    [Fact]
    public void Render_ReturnsTheRenderAreaAndDisposeIsSafe() {
        var cut = BoxPlot([new Pt(1, 0m)], new BoxPlotValues(10, 12, 14, 16, 20));
        var series = SeriesOf<NTBoxPlotSeries<Pt>>(cut);
        using var surface = NewSurface();

        var result = series.Render(CreateContext(cut.Instance, surface), Area);
        var dispose = () => { series.Dispose(); series.Dispose(); };

        result.Should().Be(Area);
        dispose.Should().NotThrow();
    }

    [Fact]
    public void Render_DrawsTheBoxBetweenTheQuartiles() {
        var cut = BoxPlot(Three, Values);

        using var surface = Draw(cut, SeriesOf<NTBoxPlotSeries<Pt>>(cut));

        var inside = At(cut, 2, 25m, Slot(cut) * 0.2f);
        var boxFill = PixelAt(surface, inside);
        IsWhite(boxFill).Should().BeFalse("the box is filled between the quartiles");
        IsClose(boxFill, Red, 0).Should().BeFalse("the fill is translucent");
        HasColorNear(surface, At(cut, 2, 40m, Slot(cut) * 0.2f), Red).Should().BeTrue("the upper quartile edge is stroked");
        HasColorNear(surface, At(cut, 2, 20m, Slot(cut) * 0.2f), Red).Should().BeTrue("the lower quartile edge is stroked");
        IsWhite(PixelAt(surface, At(cut, 2, 45m, Slot(cut) * 0.2f))).Should().BeTrue("the box does not extend past the quartiles");
    }

    [Fact]
    public void Render_DrawsTheMedianAcrossTheBox() {
        var cut = BoxPlot(Three, Values);

        using var surface = Draw(cut, SeriesOf<NTBoxPlotSeries<Pt>>(cut));

        HasColorNear(surface, At(cut, 2, 30m, Slot(cut) * 0.25f), Red).Should().BeTrue();
        HasColorNear(surface, At(cut, 2, 30m, -Slot(cut) * 0.25f), Red).Should().BeTrue();
        HasColorNear(surface, At(cut, 2, 30m, Slot(cut) * 0.4f), Red).Should().BeFalse("the default box is 0.6 of the slot wide");
    }

    [Fact]
    public void Render_DrawsWhiskersToTheMinimumAndMaximumWithCaps() {
        var cut = BoxPlot(Three, Values);

        using var surface = Draw(cut, SeriesOf<NTBoxPlotSeries<Pt>>(cut));

        HasColorNear(surface, At(cut, 2, 45m), Red, 1).Should().BeTrue("upper whisker");
        HasColorNear(surface, At(cut, 2, 15m), Red, 1).Should().BeTrue("lower whisker");
        HasColorNear(surface, At(cut, 2, 50m, Slot(cut) * 0.1f), Red, 1).Should().BeTrue("upper cap");
        HasColorNear(surface, At(cut, 2, 10m, -Slot(cut) * 0.1f), Red, 1).Should().BeTrue("lower cap");
        IsWhite(PixelAt(surface, At(cut, 2, 51m))).Should().BeTrue("nothing above the maximum");
    }

    [Fact]
    public void Render_BoxWidthRatio_SetsTheBoxWidth() {
        var narrow = BoxPlot(Three, Values, ("BoxWidthRatio", 0.2f));
        var wide = BoxPlot(Three, Values, ("BoxWidthRatio", 1f));
        using var narrowSurface = Draw(narrow, SeriesOf<NTBoxPlotSeries<Pt>>(narrow));
        using var wideSurface = Draw(wide, SeriesOf<NTBoxPlotSeries<Pt>>(wide));
        var dx = Slot(narrow) * 0.35f;

        HasColorNear(narrowSurface, At(narrow, 2, 30m, dx), Red, 1).Should().BeFalse();
        HasColorNear(wideSurface, At(wide, 2, 30m, dx), Red, 1).Should().BeTrue();
    }

    [Fact]
    public void Render_WhiskerWidthRatio_SetsTheCapWidth() {
        var narrow = BoxPlot(Three, Values, ("WhiskerWidthRatio", 0.2f));
        var wide = BoxPlot(Three, Values, ("WhiskerWidthRatio", 1f));
        using var narrowSurface = Draw(narrow, SeriesOf<NTBoxPlotSeries<Pt>>(narrow));
        using var wideSurface = Draw(wide, SeriesOf<NTBoxPlotSeries<Pt>>(wide));
        var dx = Slot(narrow) * 0.25f;

        HasColorNear(narrowSurface, At(narrow, 2, 50m, dx), Red, 1).Should().BeFalse();
        HasColorNear(wideSurface, At(wide, 2, 50m, dx), Red, 1).Should().BeTrue();
    }

    [Fact]
    public void Render_DrawsOutliersAsRingsBeyondTheWhiskers() {
        var with = BoxPlot([new Pt(1, 0m)], new BoxPlotValues(10, 20, 30, 40, 50, [65m]));
        using var withSurface = Draw(with, SeriesOf<NTBoxPlotSeries<Pt>>(with));
        var ring = At(with, 1, 65m, 3f);
        var centre = At(with, 1, 65m);

        HasColorNear(withSurface, ring, Red, 1).Should().BeTrue();
        IsWhite(PixelAt(withSurface, centre)).Should().BeTrue("the outlier is a ring, not a disc");
    }

    [Fact]
    public void Render_WithNoData_DrawsNothing() {
        var cut = BoxPlot([], Values);

        using var surface = Draw(cut, SeriesOf<NTBoxPlotSeries<Pt>>(cut));

        CountDrawnPixels(surface).Should().Be(0);
    }

    [Fact]
    public void Render_WhenHidden_DrawsNothingAndHitsNothing() {
        var cut = BoxPlot(Three, Values, ("Visible", false));
        var series = SeriesOf<NTBoxPlotSeries<Pt>>(cut);

        using var surface = Draw(cut, series);

        CountDrawnPixels(surface).Should().Be(0);
        series.HitTest(At(cut, 2, 30m), Area).Should().BeNull();
    }

    [Fact]
    public void Render_UsesTheDataPointRenderColor() {
        var cut = BoxPlot(Three, Values);
        var series = SeriesOf<NTBoxPlotSeries<Pt>>(cut);
        series.OnDataPointRender = args => args.Color = args.Index == 1 ? Green : null;

        using var surface = Draw(cut, series);

        HasColorNear(surface, At(cut, 2, 30m, Slot(cut) * 0.25f), Green).Should().BeTrue();
        HasColorNear(surface, At(cut, 1, 30m, Slot(cut) * 0.25f), Red).Should().BeTrue();
    }

    [Fact]
    public void HitTest_InsideABox_ReturnsThatBox() {
        var cut = BoxPlot(Three, Values);
        var series = SeriesOf<NTBoxPlotSeries<Pt>>(cut);
        using var surface = Draw(cut, series);

        var hit = series.HitTest(At(cut, 2, 30m), Area)!.Value;

        hit.Index.Should().Be(1);
        hit.Data.Should().Be(new Pt(2, 0m));
    }

    [Fact]
    public void HitTest_OnAWhisker_ReturnsThatBox() {
        var cut = BoxPlot(Three, Values);
        var series = SeriesOf<NTBoxPlotSeries<Pt>>(cut);
        using var surface = Draw(cut, series);

        series.HitTest(At(cut, 3, 47m), Area)!.Value.Index.Should().Be(2);
    }

    [Fact]
    public void HitTest_AwayFromAnyBox_ReturnsNull() {
        var cut = BoxPlot(Three, Values);
        var series = SeriesOf<NTBoxPlotSeries<Pt>>(cut);
        using var surface = Draw(cut, series);

        series.HitTest(At(cut, 2, 55m), Area).Should().BeNull("above the upper whisker");
        series.HitTest(At(cut, 2, 5m), Area).Should().BeNull("below the lower whisker");
        series.HitTest(At(cut, 2, 30m, Slot(cut) * 0.4f), Area).Should().BeNull("beside the box");
        series.HitTest(new SKPoint(2, 2), Area).Should().BeNull();
    }

    [Fact]
    public void HitTest_WithNoRenderedBoxes_ReturnsNull() {
        var cut = BoxPlot([new Pt(1, 0m)], Values);

        SeriesOf<NTBoxPlotSeries<Pt>>(cut).HitTest(At(cut, 1, 30m), Area).Should().BeNull();
    }

    [Fact]
    public void Tooltip_ListsTheFiveNumberSummaryAndOutliers() {
        var data = new Pt(1, 0m);
        var cut = BoxPlot([data], new BoxPlotValues(10, 20, 30, 40, 50, [65m, 70m]));

        var info = Tooltip(SeriesOf<NTBoxPlotSeries<Pt>>(cut), data);

        info.Lines.Select(l => (l.Label, l.Value)).Should().Equal(("Max", "50"), ("Q3", "40"), ("Median", "30"), ("Q1", "20"), ("Min", "10"), ("Outliers", "2"));
    }

    [Fact]
    public void Render_ReusesTheBoxesUntilInvalidated() {
        var calls = 0;
        var cut = RenderChart(XAxisKind.Int, SeriesFragment<NTBoxPlotSeries<Pt>>(Three, Selector(_ => { calls++; return Values; })));
        var series = SeriesOf<NTBoxPlotSeries<Pt>>(cut);
        using var surface = NewSurface();
        var context = CreateContext(cut.Instance, surface);

        series.Render(context, Area);
        var afterFirst = calls;
        series.Render(context, Area);
        var afterSecond = calls;
        series.Invalidate();
        series.Render(context, Area);

        afterFirst.Should().BeGreaterThan(0);
        afterSecond.Should().Be(afterFirst);
        calls.Should().BeGreaterThan(afterSecond);
    }

    [Fact]
    public void Render_OnTheSecondaryAxis_UsesTheSecondaryRange() {
        var primary = SeriesFragment<NTBoxPlotSeries<Pt>>([new Pt(1, 0m)], Selector(_ => Values), ("Color", NTColor.Primary));
        var secondary = SeriesFragment<NTBoxPlotSeries<Pt>>([new Pt(1, 0m)], Selector(_ => new BoxPlotValues(1000, 2000, 3000, 4000, 5000)), ("UseSecondaryYAxis", true), ("Color", NTColor.Secondary));
        var cut = RenderChart(new ChartSetup { SecondaryYAxis = true }, primary, secondary);

        using var surface = Draw(cut, SeriesOf<NTBoxPlotSeries<Pt>>(cut, 1));

        HasColorNear(surface, ExpectedPoint(cut.Instance, 1, 3000m, secondary: true) with { X = ExpectedPoint(cut.Instance, 1, 3000m).X + 5 }, Blue).Should().BeTrue();
    }

    private sealed class ClockedBoxPlot : NTBoxPlotSeries<Pt> {
        public void SetAnimationAge(TimeSpan age) => AnimationStartTime = DateTime.Now - age;
    }

    private (IRenderedComponent<NTChart<Pt>> Cut, ClockedBoxPlot Series) Animated(bool enabled = true) {
        var cut = RenderChart(XAxisKind.Int, SeriesFragment<ClockedBoxPlot>(Three, Selector(_ => Values), ("Color", NTColor.Primary), ("AnimationEnabled", enabled)));
        return (cut, SeriesOf<ClockedBoxPlot>(cut));
    }

    private static SKSurface DrawAt(IRenderedComponent<NTChart<Pt>> cut, ClockedBoxPlot series, TimeSpan age) {
        series.SetAnimationAge(age);
        return Draw(cut, series);
    }

    [Fact]
    public void Animation_MidwayKeepsTheQuartilesCloserToTheMedianThanFinal() {
        var (cut, series) = Animated();
        var q3 = At(cut, 2, 40m);
        var median = At(cut, 2, 30m);

        using var mid = DrawAt(cut, series, TimeSpan.FromMilliseconds(25));
        using var final = DrawAt(cut, series, TimeSpan.FromSeconds(10));

        HasColorNear(mid, q3, Red).Should().BeFalse("the upper edge is still near the median");
        HasColorNear(mid, median, Red).Should().BeTrue();
        HasColorNear(final, q3, Red).Should().BeTrue();
    }

    [Fact]
    public void Animation_Disabled_DrawsTheFinalBoxImmediately() {
        var (cut, series) = Animated(false);
        using var surface = Draw(cut, series);

        HasColorNear(surface, At(cut, 2, 40m), Red).Should().BeTrue();
        HasColorNear(surface, At(cut, 2, 50m), Red).Should().BeTrue("the upper cap is drawn");
        series.RequiresAnimationFrame.Should().BeFalse();
    }

    [Fact]
    public void Animation_RequestsFramesOnlyUntilItCompletes() {
        var (_, series) = Animated();

        series.RequiresAnimationFrame.Should().BeTrue();
        series.SetAnimationAge(TimeSpan.FromSeconds(10));
        series.RequiresAnimationFrame.Should().BeFalse();
    }

    [Fact]
    public void Animation_HitTestUsesTheFinalGeometry() {
        var (cut, series) = Animated();
        using var surface = DrawAt(cut, series, TimeSpan.FromMilliseconds(25));

        series.HitTest(At(cut, 2, 48m), Area)?.Index.Should().Be(1);
    }

    [Fact]
    public void Animation_FramesDoNotAllocate() {
        var (cut, series) = Animated();
        using var surface = NewSurface();
        var context = CreateContext(cut.Instance, surface);
        series.SetAnimationAge(TimeSpan.FromMilliseconds(100));
        series.Render(context, Area);
        series.Render(context, Area);

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 50; i++) {
            series.SetAnimationAge(TimeSpan.FromMilliseconds(100));
            series.Render(context, Area);
        }

        (GC.GetAllocatedBytesForCurrentThread() - before).Should().Be(0);
    }
}
