using Microsoft.AspNetCore.Components;
using NTComponents.Charts.Core;
using SkiaSharp;

namespace NTComponents.Charts.Tests.ChartCore;

public class NTChartAnnotation_Rendering_Tests : ChartTestBase {
    private static readonly SKColor Red = new(255, 0, 0);
    private static readonly SKColor Blue = new(0, 0, 255);

    private static RenderFragment NoAxes() => Combine(XAxis<double>(p => p.X, Args(("Visible", false))), YAxis(Args(("Visible", false))));

    private async Task<(IRenderedComponent<NTChart<Pt>> Chart, SKBitmap Bitmap, SKRect Plot)> Frame(IReadOnlyList<NTChartAnnotation> annotations, RenderFragment? content = null, ChartMargin? margin = null) {
        var chart = RenderChart(content ?? Combine(NoAxes(), Recording("a")), p => {
            p.Add(c => c.Annotations, annotations);
            if (margin.HasValue) {
                p.Add(c => c.Margin, margin.Value);
            }
        });
        var surface = await Paint(chart);
        return (chart, Bitmap(surface), PlotArea(chart));
    }

    [Fact]
    public async Task XLine_IsDrawnAtTheScaledXPositionSpanningThePlotHeight() {
        var (chart, bitmap, plot) = await Frame([new() { Type = NTChartAnnotationType.XLine, X = 1.5d }]);
        var x = (int)chart.Instance.ScaleX(1.5, plot);

        InkAt(bitmap, x, (int)plot.MidY, Red).Should().BeGreaterThan(0.5f);
        InkAt(bitmap, x, (int)plot.Top + 3, Red).Should().BeGreaterThan(0.5f);
        InkAt(bitmap, x, (int)plot.Bottom - 3, Red).Should().BeGreaterThan(0.5f);
        InkAt(bitmap, x + 12, (int)plot.MidY, Red).Should().Be(0f);
        bitmap.Dispose();
    }

    [Fact]
    public async Task YLine_IsDrawnAtTheScaledYPositionSpanningThePlotWidth() {
        var (chart, bitmap, plot) = await Frame([new() { Type = NTChartAnnotationType.YLine, Y = 25m }]);
        var y = (int)chart.Instance.ScaleY(25m, plot);

        InkAt(bitmap, (int)plot.MidX, y, Red).Should().BeGreaterThan(0.5f);
        InkAt(bitmap, (int)plot.Left + 3, y, Red).Should().BeGreaterThan(0.5f);
        InkAt(bitmap, (int)plot.Right - 3, y, Red).Should().BeGreaterThan(0.5f);
        InkAt(bitmap, (int)plot.MidX, y + 12, Red).Should().Be(0f);
        bitmap.Dispose();
    }

    [Fact]
    public async Task XRange_FillsBetweenTheBoundsWithATranslucentColorAndOutlinesBothEdges() {
        var (chart, bitmap, plot) = await Frame([new() { Type = NTChartAnnotationType.XRange, X = 1d, X2 = 2d }]);
        var left = chart.Instance.ScaleX(1, plot);
        var right = chart.Instance.ScaleX(2, plot);
        var middle = (int)((left + right) / 2);

        InkAt(bitmap, middle, (int)plot.MidY, Red, 0).Should().BeInRange(0.1f, 0.3f);
        InkAt(bitmap, (int)left - 15, (int)plot.MidY, Red, 0).Should().Be(0f);
        InkAt(bitmap, (int)right + 15, (int)plot.MidY, Red, 0).Should().Be(0f);
        InkAt(bitmap, (int)left, (int)plot.MidY, Red).Should().BeGreaterThan(0.5f);
        InkAt(bitmap, (int)right, (int)plot.MidY, Red).Should().BeGreaterThan(0.5f);
        bitmap.Dispose();
    }

    [Fact]
    public async Task XRange_AcceptsBoundsInEitherOrder() {
        var (chart, bitmap, plot) = await Frame([new() { Type = NTChartAnnotationType.XRange, X = 2d, X2 = 1d }]);
        var middle = (int)((chart.Instance.ScaleX(1, plot) + chart.Instance.ScaleX(2, plot)) / 2);

        InkAt(bitmap, middle, (int)plot.MidY, Red, 0).Should().BeInRange(0.1f, 0.3f);
        bitmap.Dispose();
    }

    [Fact]
    public async Task YRange_FillsBetweenTheBoundsAcrossThePlotWidth() {
        var (chart, bitmap, plot) = await Frame([new() { Type = NTChartAnnotationType.YRange, Y = 15m, Y2 = 30m }]);
        var middle = (int)((chart.Instance.ScaleY(15m, plot) + chart.Instance.ScaleY(30m, plot)) / 2);

        InkAt(bitmap, (int)plot.MidX, middle, Red, 0).Should().BeInRange(0.1f, 0.3f);
        InkAt(bitmap, (int)plot.MidX, (int)chart.Instance.ScaleY(30m, plot) - 15, Red, 0).Should().Be(0f);
        InkAt(bitmap, (int)plot.MidX, (int)chart.Instance.ScaleY(15m, plot), Red).Should().BeGreaterThan(0.5f);
        bitmap.Dispose();
    }

    [Fact]
    public async Task ExplicitFillColor_IsUsedForRangesAtFullOpacity() {
        var (chart, bitmap, plot) = await Frame([new() { Type = NTChartAnnotationType.XRange, X = 1d, X2 = 2d, FillColor = NTColor.Secondary }]);
        var middle = (int)((chart.Instance.ScaleX(1, plot) + chart.Instance.ScaleX(2, plot)) / 2);

        InkAt(bitmap, middle, (int)plot.MidY, Blue, 0).Should().BeGreaterThan(0.9f);
        bitmap.Dispose();
    }

    [Fact]
    public async Task Point_DrawsAMarkerRingAroundTheScaledCoordinate() {
        var (chart, bitmap, plot) = await Frame([new() { Type = NTChartAnnotationType.Point, X = 1d, Y = 25m, MarkerSize = 6f }]);
        var x = (int)Math.Round(chart.Instance.ScaleX(1, plot));
        var y = (int)Math.Round(chart.Instance.ScaleY(25m, plot));

        InkAt(bitmap, x + 6, y, Red, 1).Should().BeGreaterThan(0.5f, "the outline sits at the marker radius");
        InkAt(bitmap, x, y, Red, 0).Should().BeInRange(0.1f, 0.3f, "the marker interior is the translucent fill");
        InkAt(bitmap, x + 14, y, Red, 1).Should().Be(0f);
        bitmap.Dispose();
    }

    [Fact]
    public async Task Opacity_ScalesTheStrokeAlpha() {
        var (chart, bitmap, plot) = await Frame([new() { Type = NTChartAnnotationType.XLine, X = 1.5d, Opacity = 0.5f, StrokeWidth = 4f }]);
        var x = (int)chart.Instance.ScaleX(1.5, plot);

        InkAt(bitmap, x, (int)plot.MidY, Red, 0).Should().BeInRange(0.4f, 0.6f);
        bitmap.Dispose();
    }

    [Fact]
    public async Task DashLength_BreaksTheLineIntoSegments() {
        var (solidChart, solidBitmap, solidPlot) = await Frame([new() { Type = NTChartAnnotationType.XLine, X = 1.5d, StrokeWidth = 3f }]);
        var (dashedChart, dashedBitmap, dashedPlot) = await Frame([new() { Type = NTChartAnnotationType.XLine, X = 1.5d, StrokeWidth = 3f, DashLength = 10f }]);

        int Inked(NTChart<Pt> chart, SKBitmap bitmap, SKRect plot) {
            var x = (int)chart.ScaleX(1.5, plot);
            return Enumerable.Range((int)plot.Top, (int)plot.Height).Count(y => InkAt(bitmap, x, y, Red, 0) > 0.5f);
        }

        var solid = Inked(solidChart.Instance, solidBitmap, solidPlot);
        var dashed = Inked(dashedChart.Instance, dashedBitmap, dashedPlot);
        dashed.Should().BeLessThan((int)(solid * 0.75));
        dashed.Should().BeGreaterThan((int)(solid * 0.3));
        solidBitmap.Dispose();
        dashedBitmap.Dispose();
    }

    [Fact]
    public async Task Label_AddsABackgroundBadgeNearTheTopOfTheLine() {
        var (chart, withoutLabel, plot) = await Frame([new() { Type = NTChartAnnotationType.XLine, X = 1.5d }]);
        var (_, withLabel, _) = await Frame([new() { Type = NTChartAnnotationType.XLine, X = 1.5d, Label = "Limit" }]);
        var x = (int)chart.Instance.ScaleX(1.5, plot);
        var region = new SKRectI(x - 40, (int)plot.Top, x + 40, (int)plot.Top + 30);

        var plain = CountColor(withoutLabel, Red, 120);
        var labelled = CountColor(withLabel, Red, 120);

        labelled.Should().BeGreaterThan(plain + 100);
        BoundsOf(withLabel, new SKColor(255, 115, 115), 120, region).Should().NotBeNull();
        withoutLabel.Dispose();
        withLabel.Dispose();
    }

    [Fact]
    public async Task LabelOffsets_MoveTheBadge() {
        var (chart, baseline, plot) = await Frame([new() { Type = NTChartAnnotationType.XLine, X = 1.5d, Label = "Limit", StrokeColor = NTColor.Secondary }]);
        var (_, shifted, _) = await Frame([new() { Type = NTChartAnnotationType.XLine, X = 1.5d, Label = "Limit", StrokeColor = NTColor.Secondary, LabelOffsetX = 100f, LabelOffsetY = 40f }]);
        var x = (int)chart.Instance.ScaleX(1.5, plot);
        var badge = new SKColor(115, 115, 255);

        var a = BoundsOf(baseline, badge, 100, new SKRectI(x - 60, 0, x + 60, 200));
        var b = BoundsOf(shifted, badge, 100, new SKRectI(x + 20, 0, x + 220, 200));

        a.Should().NotBeNull();
        b.Should().NotBeNull();
        (b!.Value.Left - a!.Value.Left).Should().BeCloseTo(100, 2);
        (b.Value.Top - a.Value.Top).Should().BeCloseTo(40, 2);
        baseline.Dispose();
        shifted.Dispose();
    }

    [Fact]
    public async Task Annotation_WithMissingCoordinate_IsSkipped() {
        var (_, bitmap, _) = await Frame([new() { Type = NTChartAnnotationType.XLine, X = null }, new() { Type = NTChartAnnotationType.Point, X = 1d, Y = null }]);

        CountColor(bitmap, Red, 60).Should().Be(0);
        bitmap.Dispose();
    }

    [Fact]
    public async Task ClipToPlotArea_ControlsWhetherAnnotationsCanDrawOutsideThePlot() {
        var margin = new ChartMargin(5, 60, 5, 5);
        var (chart, clipped, plot) = await Frame([new() { Type = NTChartAnnotationType.XLine, X = 3.3d, ClipToPlotArea = true }], margin: margin);
        var (_, unclipped, _) = await Frame([new() { Type = NTChartAnnotationType.XLine, X = 3.3d, ClipToPlotArea = false }], margin: margin);
        var x = (int)chart.Instance.ScaleX(3.3, plot);

        x.Should().BeGreaterThan((int)plot.Right);
        InkAt(clipped, x, (int)plot.MidY, Red).Should().Be(0f);
        InkAt(unclipped, x, (int)plot.MidY, Red).Should().BeGreaterThan(0.5f);
        clipped.Dispose();
        unclipped.Dispose();
    }

    [Fact]
    public async Task SecondaryAxisAnnotation_UsesTheSecondaryScale() {
        var secondaryData = new Pt[] { new("A", 0, 1000m), new("B", 1, 2000m) };
        var content = Combine(XAxis<double>(p => p.X, Args(("Visible", false))), YAxis(Args(("Visible", false))), YAxis(Args(("Visible", false))),
            Recording("a"), Recording("b", data: secondaryData, extra: Args(("UseSecondaryYAxis", true))));
        var (_, bitmap, plot) = await Frame([new() { Type = NTChartAnnotationType.YLine, Y = 1500m, UseSecondaryYAxis = true }], content);

        // 1500 is the midpoint of the padded secondary range 950..2050, so it lands in the vertical center.
        InkAt(bitmap, (int)plot.MidX, (int)plot.MidY, Red, 1).Should().BeGreaterThan(0.5f);
        bitmap.Dispose();
    }

    [Fact]
    public async Task CategoryAnnotation_ResolvesTheCategoryIndexPosition() {
        var chart = RenderChart(Line(x: p => p.Name), p => p.Add(c => c.Annotations, [new NTChartAnnotation { Type = NTChartAnnotationType.XLine, X = "C" }]));
        using var surface = await Paint(chart);
        using var bitmap = Bitmap(surface);
        var plot = PlotArea(chart);
        var x = (int)chart.Instance.ScaleX(2, plot);

        InkAt(bitmap, x, (int)plot.Top + 3, Red).Should().BeGreaterThan(0.5f);
    }

    [Fact]
    public async Task DateAnnotation_ResolvesTheTickPosition() {
        var feb = new DateTime(2024, 2, 20);
        var data = new Pt[] { new("a", 0, 5m, new DateTime(2024, 1, 15)), new("b", 1, 7m, feb), new("c", 2, 3m, new DateTime(2024, 3, 25)) };
        var chart = RenderChart(Combine(XAxis<DateTime>(p => p.Date, Args(("Visible", false))), YAxis(Args(("Visible", false))), Line(data, x: p => p.Date, extra: Args(("Color", (NTColor?)NTColor.Secondary)))),
            p => p.Add(c => c.Annotations, [new NTChartAnnotation { Type = NTChartAnnotationType.XLine, X = feb, StrokeColor = NTColor.Primary }]));
        using var surface = await Paint(chart);
        using var bitmap = Bitmap(surface);
        var plot = PlotArea(chart);
        var x = (int)chart.Instance.ScaleX(feb.Ticks, plot);

        InkAt(bitmap, x, (int)plot.Top + 3, Red).Should().BeGreaterThan(0.5f);
    }

    [Fact]
    public async Task Annotations_AreDrawnOverTheSeries() {
        var content = Combine(NoAxes(), Bar());
        var (chart, bitmap, plot) = await Frame([new() { Type = NTChartAnnotationType.XLine, X = 1d, StrokeColor = NTColor.Secondary, StrokeWidth = 4f }], content);
        var x = (int)chart.Instance.ScaleX(1, plot);
        var y = (int)plot.Bottom - 30;

        InkAt(bitmap, x, y, Blue, 0).Should().BeGreaterThan(0.8f, "the annotation line must paint over the bar fill");
        InkAt(bitmap, x + 12, y, Red, 0).Should().BeGreaterThan(0.8f, "the bar itself is drawn next to the line");
        bitmap.Dispose();
    }

    [Fact]
    public async Task CustomRenderer_ReceivesPlotAreaDensityAndWorkingScaleDelegates() {
        NTChartAnnotationRenderContext? received = null;
        float? scaledX = null;
        float? scaledY = null;
        SKColor? themeColor = null;
        var annotation = new NTChartAnnotation {
            Type = NTChartAnnotationType.Custom,
            X = 1d,
            Y = 25m,
            CustomRenderer = context => {
                received = context;
                scaledX = context.ScaleX(1d);
                scaledY = context.ScaleY(25m, false);
                themeColor = context.ResolveThemeColor(NTColor.Secondary);
                using var paint = new SKPaint { Color = SKColors.Blue };
                context.Canvas.DrawRect(new SKRect(0, 0, 640, 320), paint);
            }
        };
        var (chart, bitmap, plot) = await Frame([annotation]);

        received.Should().NotBeNull();
        received!.PlotArea.Should().Be(plot);
        received.Density.Should().Be(1f);
        received.Annotation.Should().BeSameAs(annotation);
        scaledX.Should().BeApproximately(chart.Instance.ScaleX(1, plot), 0.01f);
        scaledY.Should().BeApproximately(chart.Instance.ScaleY(25m, plot), 0.01f);
        themeColor.Should().Be(Blue);
        InkAt(bitmap, (int)plot.MidX, (int)plot.MidY, Blue, 0).Should().BeGreaterThan(0.99f);
        bitmap.GetPixel(1, 1).Should().Be(SKColors.White, "ClipToPlotArea confines custom drawing to the plot");
        bitmap.Dispose();
    }

    [Fact]
    public async Task CustomRenderer_OnBuiltInTypeRunsAfterTheBuiltInDrawing() {
        var order = new List<string>();
        var annotation = new NTChartAnnotation { Type = NTChartAnnotationType.XLine, X = 1d, CustomRenderer = _ => order.Add("custom") };
        var (_, bitmap, _) = await Frame([annotation]);

        order.Should().Equal("custom");
        CountColor(bitmap, Red, 60).Should().BeGreaterThan(0, "the built-in line is drawn as well");
        bitmap.Dispose();
    }

    [Fact]
    public async Task EmptyAnnotationList_DrawsNothingExtra() {
        var (_, bitmap, _) = await Frame([]);

        CountColor(bitmap, Red, 60).Should().Be(0);
        bitmap.Dispose();
    }
}
