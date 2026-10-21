using NTComponents.Charts.Core;
using SkiaSharp;

namespace NTComponents.Charts.Tests.Series.Cartesian;

public class NTHeatMapSeries_Tests : CartesianChartTestBase {
    private static (string, object) Weight(Func<Pt, decimal> selector) => ("WeightSelector", selector);

    private static Pt[] Row(params decimal[] weights) => weights.Select((w, i) => new Pt(i + 1, 1m, w)).ToArray();

    private IRenderedComponent<NTChart<Pt>> HeatMap(Pt[] data, params (string, object)[] overrides) =>
        RenderChart(XAxisKind.Int, SeriesFragment<NTHeatMapSeries<Pt>>(data, [Weight(p => p.Z), .. overrides]));

    private static SKColor Pixel(SKSurface surface, IRenderedComponent<NTChart<Pt>> cut, double x, decimal y, float dx = 0) {
        var point = ExpectedPoint(cut.Instance, x, y);
        return PixelAt(surface, new SKPoint(point.X + dx, point.Y));
    }

    [Fact]
    public void NewInstance_HasDocumentedDefaults() {
        var series = new NTHeatMapSeries<Pt>();

        series.MinColor.Should().Be(NTColor.SurfaceContainerHighest, "the low end must stay visible against Surface");
        series.MaxColor.Should().Be(NTColor.Primary);
        series.CellPadding.Should().Be(2f, "the gap between cells is a fixed 2dp");
    }

    [Fact]
    public void Ranges_AreWidenedByHalfACell() {
        var data = new[] { new Pt(1, 5m, 1m), new Pt(4, -2m, 9m) };
        var series = SeriesOf<NTHeatMapSeries<Pt>>(HeatMap(data));

        series.GetXRange().Should().Be((-0.5d, 5.5d));
        series.GetYRange().Should().Be((-5.5m, 8.5m));
    }

    [Fact]
    public void Ranges_UseTheSmallestGapBetweenDistinctValues() {
        var data = new[] { new Pt(1, 0m, 1m), new Pt(2, 10m, 1m), new Pt(4, 12m, 1m), new Pt(4, 12m, 2m) };
        var series = SeriesOf<NTHeatMapSeries<Pt>>(HeatMap(data));

        series.GetXRange().Should().Be((0.5d, 4.5d));
        series.GetYRange().Should().Be((-1m, 13m));
    }

    [Fact]
    public void Ranges_WithASingleRow_SpanOneBand() {
        var series = SeriesOf<NTHeatMapSeries<Pt>>(HeatMap(Row(1m)));

        series.GetYRange().Should().Be((0.5m, 1.5m));
        series.GetXRange().Should().Be((0.5d, 1.5d));
    }

    [Fact]
    public void Ranges_WithNoData_AreNull() {
        var series = SeriesOf<NTHeatMapSeries<Pt>>(HeatMap([]));

        series.GetXRange().Should().BeNull();
        series.GetYRange().Should().BeNull();
    }

    [Fact]
    public void Render_ReturnsTheRenderAreaAndDisposeIsSafe() {
        var cut = HeatMap(Row(1m));
        var series = SeriesOf<NTHeatMapSeries<Pt>>(cut);
        using var surface = NewSurface();

        var result = series.Render(CreateContext(cut.Instance, surface), Area);
        var dispose = () => { series.Dispose(); series.Dispose(); };

        result.Should().Be(Area);
        dispose.Should().NotThrow();
    }

    [Fact]
    public void Render_ColorsCellsBetweenMinAndMaxColorByWeight() {
        var cut = HeatMap([new Pt(1, 1m, 0m), new Pt(2, 1m, 10m)], ("MinColor", NTColor.Secondary), ("MaxColor", NTColor.Primary));

        using var surface = Draw(cut, SeriesOf<NTHeatMapSeries<Pt>>(cut));

        IsClose(Pixel(surface, cut, 1, 1m), Blue).Should().BeTrue();
        IsClose(Pixel(surface, cut, 2, 1m), Red).Should().BeTrue();
    }

    [Fact]
    public void Render_ColorsAMidWeightHalfwayBetweenTheColors() {
        var cut = HeatMap(Row(0m, 5m, 10m), ("MinColor", NTColor.Secondary), ("MaxColor", NTColor.Primary));

        using var surface = Draw(cut, SeriesOf<NTHeatMapSeries<Pt>>(cut));

        IsClose(Pixel(surface, cut, 2, 1m), new SKColor(128, 0, 128)).Should().BeTrue();
    }

    [Fact]
    public void Render_WithOneWeightValue_UsesTheMaxColorForEveryCell() {
        var cut = HeatMap(Row(7m, 7m), ("MinColor", NTColor.Secondary), ("MaxColor", NTColor.Primary));

        using var surface = Draw(cut, SeriesOf<NTHeatMapSeries<Pt>>(cut));

        IsClose(Pixel(surface, cut, 1, 1m), Red).Should().BeTrue();
        IsClose(Pixel(surface, cut, 2, 1m), Red).Should().BeTrue();
    }

    [Fact]
    public void Render_WithNoData_DrawsNothing() {
        var cut = HeatMap([]);

        using var surface = Draw(cut, SeriesOf<NTHeatMapSeries<Pt>>(cut));

        CountDrawnPixels(surface).Should().Be(0);
    }

    [Fact]
    public void Render_WhenHidden_DrawsNothingAndHitsNothing() {
        var cut = HeatMap(Row(0m, 10m), ("Visible", false));
        var series = SeriesOf<NTHeatMapSeries<Pt>>(cut);

        using var surface = Draw(cut, series);

        CountDrawnPixels(surface).Should().Be(0);
        series.HitTest(ExpectedPoint(cut.Instance, 1, 1m), Area).Should().BeNull();
    }

    [Fact]
    public void Render_CellPadding_LeavesAGapAroundEachCell() {
        var data = Row(10m, 10m);
        var tight = HeatMap(data, ("CellPadding", 0f));
        var unit = ExpectedPoint(tight.Instance, 2, 1m).X - ExpectedPoint(tight.Instance, 1, 1m).X;
        var padded = HeatMap(data, ("CellPadding", unit * 0.5f));

        using var tightSurface = Draw(tight, SeriesOf<NTHeatMapSeries<Pt>>(tight));
        using var paddedSurface = Draw(padded, SeriesOf<NTHeatMapSeries<Pt>>(padded));

        IsWhite(Pixel(tightSurface, tight, 1, 1m, unit * 0.45f)).Should().BeFalse();
        IsWhite(Pixel(paddedSurface, padded, 1, 1m, unit * 0.45f)).Should().BeTrue();
        IsWhite(Pixel(paddedSurface, padded, 1, 1m, unit * 0.2f)).Should().BeFalse();
    }

    [Theory]
    [InlineData(2)]
    [InlineData(5)]
    public void Render_DefaultGap_IsAFixedTwoPixelsRegardlessOfCellSize(int cells) {
        var cut = HeatMap(Row(Enumerable.Repeat(10m, cells).ToArray()));
        var first = ExpectedPoint(cut.Instance, 1, 1m);
        var second = ExpectedPoint(cut.Instance, 2, 1m);

        using var surface = Draw(cut, SeriesOf<NTHeatMapSeries<Pt>>(cut));

        var gapPixels = 0;
        for (var x = (int)first.X; x < (int)second.X; x++) {
            if (IsWhite(PixelAt(surface, x, (int)first.Y))) {
                gapPixels++;
            }
        }

        gapPixels.Should().BeInRange(1, 3, "cells are separated by a fixed 2dp gap, drawn anti-aliased");
    }

    [Fact]
    public void Render_OnACategoricalAxis_DrawsOneColumnPerCategory() {
        var data = new[] { new Pt("a", 1m, 0m), new Pt("b", 1m, 10m), new Pt("a", 2m, 10m) };
        var cut = RenderChart(XAxisKind.Default, SeriesFragment<NTHeatMapSeries<Pt>>(data, Weight(p => p.Z), ("MinColor", NTColor.Secondary), ("MaxColor", NTColor.Primary)));
        var series = SeriesOf<NTHeatMapSeries<Pt>>(cut);

        using var surface = Draw(cut, series);

        IsClose(Pixel(surface, cut, 0, 1m), Blue).Should().BeTrue();
        IsClose(Pixel(surface, cut, 1, 1m), Red).Should().BeTrue();
        IsClose(Pixel(surface, cut, 0, 2m), Red).Should().BeTrue();
        series.HitTest(ExpectedPoint(cut.Instance, 1, 1m), Area)!.Value.Index.Should().Be(1);
    }

    [Fact]
    public void Render_UsesTheDataPointRenderColor() {
        var cut = HeatMap(Row(0m, 10m));
        var series = SeriesOf<NTHeatMapSeries<Pt>>(cut);
        series.OnDataPointRender = args => args.Color = args.Index == 0 ? Green : null;

        using var surface = Draw(cut, series);

        IsClose(Pixel(surface, cut, 1, 1m), Green).Should().BeTrue();
        IsClose(Pixel(surface, cut, 2, 1m), Red).Should().BeTrue();
    }

    [Fact]
    public void HitTest_InsideACell_ReturnsThatCell() {
        var cut = HeatMap([new Pt(1, 1m, 0m), new Pt(2, 1m, 10m)]);
        var series = SeriesOf<NTHeatMapSeries<Pt>>(cut);
        using var surface = Draw(cut, series);

        var hit = series.HitTest(ExpectedPoint(cut.Instance, 2, 1m), Area)!.Value;

        hit.Index.Should().Be(1);
        hit.Data.Should().Be(new Pt(2, 1m, 10m));
    }

    [Fact]
    public void HitTest_OutsideEveryCell_ReturnsNull() {
        var cut = HeatMap(Row(1m, 2m), ("CellPadding", 0.5f));
        var series = SeriesOf<NTHeatMapSeries<Pt>>(cut);
        using var surface = Draw(cut, series);
        var first = ExpectedPoint(cut.Instance, 1, 1m);
        var between = new SKPoint((first.X + ExpectedPoint(cut.Instance, 2, 1m).X) / 2, first.Y);

        series.HitTest(between, Area).Should().BeNull();
        series.HitTest(new SKPoint(1, 1), Area).Should().BeNull();
    }

    [Fact]
    public void HitTest_WithNoRenderedCells_ReturnsNull() {
        var cut = HeatMap([new Pt(1, 1m, 1m)]);

        SeriesOf<NTHeatMapSeries<Pt>>(cut).HitTest(new SKPoint(5, 5), Area).Should().BeNull();
    }

    [Fact]
    public void Tooltip_ShowsTheWeightAndTheRow() {
        var data = new Pt(2, 7m, 10m);
        var cut = HeatMap([data]);

        var info = Tooltip(SeriesOf<NTHeatMapSeries<Pt>>(cut), data);

        info.Lines.Select(l => l.Value).Should().Equal("10", "7");
    }

    [Fact]
    public void Render_ReusesTheCellsUntilInvalidated() {
        var calls = 0;
        var cut = HeatMap(Row(0m, 10m), Weight(p => { calls++; return p.Z; }));
        var series = SeriesOf<NTHeatMapSeries<Pt>>(cut);
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
        var primary = SeriesFragment<NTHeatMapSeries<Pt>>([new Pt(1, 10m, 1m), new Pt(2, 20m, 1m)], Weight(p => p.Z));
        var secondary = SeriesFragment<NTHeatMapSeries<Pt>>([new Pt(1, 1000m, 1m), new Pt(2, 2000m, 5m)], Weight(p => p.Z), ("UseSecondaryYAxis", true), ("MaxColor", NTColor.Secondary));
        var cut = RenderChart(new ChartSetup { SecondaryYAxis = true }, primary, secondary);

        using var surface = Draw(cut, SeriesOf<NTHeatMapSeries<Pt>>(cut, 1));

        IsClose(PixelAt(surface, ExpectedPoint(cut.Instance, 2, 2000m, secondary: true)), Blue).Should().BeTrue();
    }

    private sealed class ClockedHeatMap : NTHeatMapSeries<Pt> {
        public void SetAnimationAge(TimeSpan age) => AnimationStartTime = DateTime.Now - age;
    }

    private (IRenderedComponent<NTChart<Pt>> Cut, ClockedHeatMap Series) Animated(bool enabled = true) {
        var cut = RenderChart(XAxisKind.Int, SeriesFragment<ClockedHeatMap>(Row(5m), Weight(p => p.Z), ("AnimationEnabled", enabled)));
        return (cut, SeriesOf<ClockedHeatMap>(cut));
    }

    private static int DrawnAt(IRenderedComponent<NTChart<Pt>> cut, ClockedHeatMap series, TimeSpan age) {
        series.SetAnimationAge(age);
        using var surface = Draw(cut, series);
        return CountDrawnPixels(surface);
    }

    [Fact]
    public void Animation_MidwayDrawsASmallerCellThanTheFinalOne() {
        var (cut, series) = Animated();

        var mid = DrawnAt(cut, series, TimeSpan.FromMilliseconds(25));
        var final = DrawnAt(cut, series, TimeSpan.FromSeconds(10));

        mid.Should().BeGreaterThan(0).And.BeLessThan(final);
    }

    [Fact]
    public void Animation_AtProgressOne_MatchesAnimationDisabled() {
        var (cut, series) = Animated();
        var (staticCut, staticSeries) = Animated(false);

        DrawnAt(cut, series, TimeSpan.FromSeconds(10)).Should().Be(DrawnAt(staticCut, staticSeries, TimeSpan.Zero));
    }

    [Fact]
    public void Animation_Disabled_DrawsTheFinalCellImmediately() {
        var (cut, series) = Animated(false);
        using var surface = Draw(cut, series);

        IsClose(Pixel(surface, cut, 1, 1m), Red).Should().BeTrue();
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
