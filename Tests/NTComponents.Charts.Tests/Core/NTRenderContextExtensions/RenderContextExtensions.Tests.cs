using NTComponents.Charts.Core;
using NTComponents.Charts.Core.Series;
using SkiaSharp;

namespace NTComponents.Charts.Tests.ChartCore;

public class NTRenderContextExtensions_Tests : ChartTestBase {
    private static readonly SKColor Red = SKColors.Red;
    private static readonly SKColor Blue = new(0, 0, 255);

    private (NTChart<Pt> Chart, NTBaseSeries<Pt> Series, SKSurface Surface, NTRenderContext Context) Setup(float density = 1f) {
        var chart = RenderChart(Recording("a"));
        var surface = SKSurface.Create(new SKImageInfo(200, 200))!;
        surface.Canvas.Clear(SKColors.White);
        var context = new NTRenderContext {
            Canvas = surface.Canvas,
            DefaultFont = chart.Instance.DefaultFont,
            RegularFont = chart.Instance.RegularFont,
            Density = density,
            Info = new SKImageInfo(200, 200),
            PlotArea = new SKRect(0, 0, 200, 200),
            TextColor = SKColors.Black,
            TotalArea = new SKRect(0, 0, 200, 200)
        };
        return (chart.Instance, chart.Instance.Series.Single(), surface, context);
    }

    private static bool IsRed(SKBitmap bitmap, int x, int y) => InkAt(bitmap, x, y, Red, 0) > 0.9f;

    [Fact]
    public void DrawPoint_FilledCircle_CoversTheDiskAndNothingBeyondIt() {
        var (chart, series, surface, context) = Setup();
        using var _ = surface;

        context.DrawPoint(chart, series, 100, 100, Red, PointStyle.Filled, 10, PointShape.Circle);
        using var bitmap = Bitmap(surface);

        IsRed(bitmap, 100, 100).Should().BeTrue();
        IsRed(bitmap, 102, 102).Should().BeTrue();
        IsRed(bitmap, 104, 104).Should().BeFalse("the corner of the bounding box lies outside the circle");
        IsRed(bitmap, 106, 106).Should().BeFalse();
    }

    [Fact]
    public void DrawPoint_FilledSquare_CoversTheWholeBoundingBox() {
        var (chart, series, surface, context) = Setup();
        using var _ = surface;

        context.DrawPoint(chart, series, 100, 100, Red, PointStyle.Filled, 10, PointShape.Square);
        using var bitmap = Bitmap(surface);

        IsRed(bitmap, 96, 96).Should().BeTrue();
        IsRed(bitmap, 104, 104).Should().BeTrue();
        IsRed(bitmap, 107, 100).Should().BeFalse();
    }

    [Fact]
    public void DrawPoint_Triangle_PointsUpAndWidensTowardTheBottom() {
        var (chart, series, surface, context) = Setup();
        using var _ = surface;

        context.DrawPoint(chart, series, 100, 100, Red, PointStyle.Filled, 20, PointShape.Triangle);
        using var bitmap = Bitmap(surface);

        IsRed(bitmap, 100, 92).Should().BeTrue("near the apex");
        IsRed(bitmap, 92, 92).Should().BeFalse("the top corners are empty");
        IsRed(bitmap, 93, 108).Should().BeTrue("the base corners are filled");
        IsRed(bitmap, 107, 108).Should().BeTrue();
    }

    [Fact]
    public void DrawPoint_Diamond_FillsTheCentreAndLeavesTheCornersEmpty() {
        var (chart, series, surface, context) = Setup();
        using var _ = surface;

        context.DrawPoint(chart, series, 100, 100, Red, PointStyle.Filled, 20, PointShape.Diamond);
        using var bitmap = Bitmap(surface);

        IsRed(bitmap, 100, 100).Should().BeTrue();
        IsRed(bitmap, 100, 92).Should().BeTrue();
        IsRed(bitmap, 106, 106).Should().BeFalse();
        IsRed(bitmap, 108, 108).Should().BeFalse();
        IsRed(bitmap, 92, 92).Should().BeFalse();
    }

    [Fact]
    public void DrawPoint_None_DrawsNothing() {
        var (chart, series, surface, context) = Setup();
        using var _ = surface;

        context.DrawPoint(chart, series, 100, 100, Red, PointStyle.None, 10, PointShape.Circle);
        using var bitmap = Bitmap(surface);

        CountColor(bitmap, Red, 200).Should().Be(0);
    }

    [Fact]
    public void DrawPoint_Outlined_DrawsARingWithAnEmptyCentre() {
        var (chart, series, surface, context) = Setup();
        using var _ = surface;

        context.DrawPoint(chart, series, 100, 100, Red, PointStyle.Outlined, 20, PointShape.Circle);
        using var bitmap = Bitmap(surface);

        IsRed(bitmap, 100, 100).Should().BeFalse();
        InkAt(bitmap, 110, 100, Red, 1).Should().BeGreaterThan(0.8f);
    }

    [Fact]
    public void DrawPoint_OutlinedWithStrokeColor_OverpaintsTheRingInThatColor() {
        var (chart, series, surface, context) = Setup();
        using var _ = surface;

        context.DrawPoint(chart, series, 100, 100, Red, PointStyle.Outlined, 20, PointShape.Square, SKColors.Blue);
        using var bitmap = Bitmap(surface);

        InkAt(bitmap, 110, 100, Blue, 1).Should().BeGreaterThan(0.8f, "a circular outline in the stroke color is added on top");
        InkAt(bitmap, 90, 90, Red, 1).Should().BeGreaterThan(0.8f, "the square outline corner keeps the base color");
    }

    [Fact]
    public void DrawPoint_ScalesSizeByDensity() {
        var (chart, series, surface, context) = Setup(density: 2f);
        using var _ = surface;

        context.DrawPoint(chart, series, 100, 100, Red, PointStyle.Filled, 10, PointShape.Square);
        using var bitmap = Bitmap(surface);

        IsRed(bitmap, 109, 100).Should().BeTrue("a 10 logical pixel marker is 20 device pixels wide");
        IsRed(bitmap, 111, 100).Should().BeFalse();
    }

    [Fact]
    public void DrawDataLabel_WithBackground_DrawsASeriesColoredBadgeAboveThePoint() {
        var (chart, series, surface, context) = Setup();
        using var _ = surface;

        context.DrawDataLabel(chart, series, 100, 100, 42m, new SKRect(0, 0, 200, 200), "{0:0}");
        using var bitmap = Bitmap(surface);
        var badge = BoundsOf(bitmap, Red, 10)!.Value;

        badge.Bottom.Should().BeLessThan(100, "centered labels sit above the anchor");
        ((badge.Left + badge.Right) / 2f).Should().BeApproximately(100f, 6f);
    }

    [Fact]
    public void DrawDataLabel_NearTheTopEdge_FlipsBelowThePoint() {
        var (chart, series, surface, context) = Setup();
        using var _ = surface;

        context.DrawDataLabel(chart, series, 100, 8, 42m, new SKRect(0, 0, 200, 200), "{0:0}");
        using var bitmap = Bitmap(surface);
        var badge = BoundsOf(bitmap, Red, 10)!.Value;

        badge.Top.Should().BeGreaterThan(8);
    }

    [Fact]
    public void DrawDataLabel_WithoutBackground_DrawsOnlyTheTextInTheGivenColor() {
        var (chart, series, surface, context) = Setup();
        using var _ = surface;

        context.DrawDataLabel(chart, series, 100, 100, 42m, new SKRect(0, 0, 200, 200), "{0:0}", textColor: SKColors.Blue, showBackground: false);
        using var bitmap = Bitmap(surface);

        CountColor(bitmap, Red, 10).Should().Be(0);
        CountColor(bitmap, Blue, 60).Should().BeGreaterThan(5);
    }

    [Fact]
    public void DrawDataLabel_CustomBackgroundColorAndFontSize_AreApplied() {
        var (chart, series, surface, context) = Setup();
        using var _ = surface;
        using var other = SKSurface.Create(new SKImageInfo(200, 200))!;
        other.Canvas.Clear(SKColors.White);
        var otherContext = new NTRenderContext {
            Canvas = other.Canvas, DefaultFont = context.DefaultFont, RegularFont = context.RegularFont, Density = 1f, Info = context.Info,
            PlotArea = context.PlotArea, TextColor = context.TextColor, TotalArea = context.TotalArea
        };

        context.DrawDataLabel(chart, series, 100, 100, 42m, new SKRect(0, 0, 200, 200), "{0:0}", backgroundColor: SKColors.Blue, fontSize: 12);
        otherContext.DrawDataLabel(chart, series, 100, 100, 42m, new SKRect(0, 0, 200, 200), "{0:0}", backgroundColor: SKColors.Blue, fontSize: 24);
        using var small = Bitmap(surface);
        using var large = Bitmap(other);

        var smallBadge = BoundsOf(small, Blue, 10)!.Value;
        var largeBadge = BoundsOf(large, Blue, 10)!.Value;
        CountColor(small, Red, 10).Should().Be(0);
        (largeBadge.Bottom - largeBadge.Top).Should().BeGreaterThan(smallBadge.Bottom - smallBadge.Top);
    }

    [Theory]
    [InlineData(ChartInteractions.PanAll, ChartInteractions.XPan | ChartInteractions.YPan)]
    [InlineData(ChartInteractions.ZoomAll, ChartInteractions.XZoom | ChartInteractions.YZoom)]
    [InlineData(ChartInteractions.All, ChartInteractions.XPan | ChartInteractions.YPan | ChartInteractions.XZoom | ChartInteractions.YZoom)]
    public void ChartInteractions_CompositeValuesCombineTheirFlags(ChartInteractions composite, ChartInteractions expected) {
        composite.Should().Be(expected);
    }

    [Fact]
    public void ChartInteractions_FlagsAreDistinctBitsAndNoneIsZero() {
        var flags = new[] { ChartInteractions.XPan, ChartInteractions.YPan, ChartInteractions.XZoom, ChartInteractions.YZoom };

        ((int)ChartInteractions.None).Should().Be(0);
        flags.Select(f => (int)f).Should().OnlyHaveUniqueItems().And.OnlyContain(v => (v & (v - 1)) == 0 && v != 0);
        ChartInteractions.All.HasFlag(ChartInteractions.YZoom).Should().BeTrue();
        ChartInteractions.PanAll.HasFlag(ChartInteractions.XZoom).Should().BeFalse();
    }

    [Fact]
    public void ChartMargin_DefaultStructHasZeroMarginsAndSettersAreIndependent() {
        var margin = default(ChartMargin);
        margin.Top = 1;
        margin.Left = 4;

        (margin.Top, margin.Right, margin.Bottom, margin.Left).Should().Be((1f, 0f, 0f, 4f));
    }
}
