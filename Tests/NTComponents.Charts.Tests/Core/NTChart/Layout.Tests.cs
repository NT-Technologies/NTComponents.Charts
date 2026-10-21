using Microsoft.AspNetCore.Components;
using NTComponents.Charts.Core;
using SkiaSharp;

namespace NTComponents.Charts.Tests.ChartCore;

public class NTChart_Layout_Tests : ChartTestBase {
    private const float Tolerance = 0.01f;

    private static readonly Dictionary<string, object> Hidden = Args(("Visible", false));

    private static RenderFragment NoAxes() => Combine(XAxis<double>(p => p.X, Hidden), YAxis(Hidden));

    private async Task<SKRect> PlotAreaOf(RenderFragment content, Action<ComponentParameterCollectionBuilder<NTChart<Pt>>>? configure = null, float density = 1f) {
        var chart = RenderChart(content, configure);
        if (density != 1f) {
            await chart.InvokeAsync(() => chart.Instance.OnNativeViewportChanged(true, density));
        }
        using var surface = await Paint(chart);
        return PlotArea(chart);
    }

    [Fact]
    public async Task HiddenAxes_PlotAreaIsTheChartMarginInset() {
        var plot = await PlotAreaOf(Combine(NoAxes(), Line()), p => p.Add(c => c.Margin, new ChartMargin(10, 20, 30, 40)));

        plot.Left.Should().BeApproximately(40, Tolerance);
        plot.Top.Should().BeApproximately(10, Tolerance);
        plot.Right.Should().BeApproximately(Width - 20, Tolerance);
        plot.Bottom.Should().BeApproximately(Height - 30, Tolerance);
    }

    [Fact]
    public async Task DefaultMargin_IsEightPixelsOnEverySide() {
        var plot = await PlotAreaOf(Combine(NoAxes(), Line()));

        plot.Should().Be(new SKRect(8, 8, Width - 8, Height - 8));
    }

    [Fact]
    public async Task Margin_ScalesWithDevicePixelDensity() {
        var plot = await PlotAreaOf(Combine(NoAxes(), Line()), p => p.Add(c => c.Margin, new ChartMargin(10, 20, 30, 40)), density: 2f);

        plot.Left.Should().BeApproximately(80, Tolerance);
        plot.Top.Should().BeApproximately(20, Tolerance);
        plot.Right.Should().BeApproximately(Width - 40, Tolerance);
        plot.Bottom.Should().BeApproximately(Height - 60, Tolerance);
    }

    [Fact]
    public void ChartMargin_All_AppliesTheSameValueToEverySide() {
        var margin = ChartMargin.All(7);

        (margin.Top, margin.Right, margin.Bottom, margin.Left).Should().Be((7f, 7f, 7f, 7f));
    }

    [Fact]
    public void ChartMargin_Constructor_MapsSidesInCssOrder() {
        var margin = new ChartMargin(1, 2, 3, 4);

        (margin.Top, margin.Right, margin.Bottom, margin.Left).Should().Be((1f, 2f, 3f, 4f));
    }

    [Fact]
    public async Task VisibleAxes_ShrinkPlotAreaOnTheLeftAndBottomOnly() {
        var hidden = await PlotAreaOf(Combine(NoAxes(), Line()));
        var visible = await PlotAreaOf(Combine(XAxis<double>(p => p.X), YAxis(), Line()));

        visible.Left.Should().BeGreaterThan(hidden.Left + 10);
        visible.Bottom.Should().BeLessThan(hidden.Bottom - 10);
        visible.Top.Should().BeApproximately(hidden.Top, Tolerance);
        visible.Right.Should().BeApproximately(hidden.Right, Tolerance);
    }

    [Fact]
    public async Task AxisTitles_ReserveAdditionalSpace() {
        var plain = await PlotAreaOf(Combine(XAxis<double>(p => p.X), YAxis(), Line()));
        var titled = await PlotAreaOf(Combine(XAxis<double>(p => p.X, Args(("Title", "Time"))), YAxis(Args(("Title", "Revenue"))), Line()));

        titled.Left.Should().BeGreaterThan(plain.Left);
        titled.Bottom.Should().BeLessThan(plain.Bottom);
    }

    [Fact]
    public async Task SecondaryYAxis_ReservesSpaceOnTheRight() {
        var single = await PlotAreaOf(Combine(XAxis<double>(p => p.X), YAxis(), Line()));
        var dual = await PlotAreaOf(Combine(XAxis<double>(p => p.X), YAxis(), YAxis(), Line(), Line(extra: Args(("UseSecondaryYAxis", true)))));

        dual.Right.Should().BeLessThan(single.Right - 10);
        dual.Left.Should().BeApproximately(single.Left, 1f);
    }

    [Fact]
    public async Task LongerAxisLabels_WidenTheYAxisGutter() {
        var shortLabels = await PlotAreaOf(Combine(YAxis(), Line()));
        var longLabels = await PlotAreaOf(Combine(YAxis(), Line([new("A", 0, 1_000_000m), new("B", 1, 9_000_000m)])));

        longLabels.Left.Should().BeGreaterThan(shortLabels.Left);
    }

    [Fact]
    public async Task SeriesRenderArea_IsExactlyThePlotArea() {
        var log = new List<string>();
        var chart = RenderChart(Combine(XAxis<double>(p => p.X), YAxis(), Recording("a", log)));
        using var surface = await Paint(chart);

        chart.Instance.Series.Single().Should().BeOfType<RecordingSeries>().Which.RenderAreas.Should().ContainSingle().Which.Should().Be(PlotArea(chart));
    }

    [Fact]
    public async Task Frame_RendersLayersInTitleLegendSeriesTooltipOrderWithCumulativeLayout() {
        var log = new List<string>();
        var chart = RenderChart(Combine(NoAxes(), Recording("series", log)));
        var title = new StubRenderable(RenderOrdered.Title, "title", log, consumeTop: 30);
        var legend = new StubRenderable(RenderOrdered.Legend, "legend", log, consumeTop: 20);
        var tooltip = new StubRenderable(RenderOrdered.Tooltip, "tooltip", log);
        chart.Instance.RegisterRenderable(tooltip);
        chart.Instance.RegisterRenderable(legend);
        chart.Instance.RegisterRenderable(title);

        using var surface = await Paint(chart);

        log.Should().Equal("title", "legend", "series", "tooltip");
        title.LastArea!.Value.Top.Should().Be(8);
        legend.LastArea!.Value.Top.Should().Be(38, "the title consumed 30px");
        var series = chart.Instance.Series.Cast<RecordingSeries>().Single();
        series.RenderAreas.Single().Top.Should().Be(58, "the legend consumed another 20px");
        tooltip.LastArea!.Value.Top.Should().Be(58);
    }

    [Fact]
    public async Task Frame_PassesDensityAndTotalAreaToRenderables() {
        var chart = RenderChart(Combine(NoAxes(), Line()));
        var tooltip = new StubRenderable(RenderOrdered.Tooltip, "tooltip");
        chart.Instance.RegisterRenderable(tooltip);
        await chart.InvokeAsync(() => chart.Instance.OnNativeViewportChanged(true, 2f));

        using var surface = await Paint(chart);

        tooltip.LastContext!.Density.Should().Be(2f);
        tooltip.LastContext.TotalArea.Should().Be(new SKRect(0, 0, Width, Height));
        tooltip.LastContext.Info.Width.Should().Be(Width);
        tooltip.LastContext.PlotArea.Should().Be(PlotArea(chart));
    }

    [Fact]
    public async Task Paint_HiddenSeriesAreSkippedAndTheirAreaIsNotRendered() {
        var log = new List<string>();
        var chart = RenderChart(Combine(NoAxes(), Recording("visible", log), Recording("hidden", log, extra: Hidden)));

        using var surface = await Paint(chart);

        log.Should().Equal("visible");
    }

    [Fact]
    public async Task Paint_UnusableSurfaceSize_IsIgnored() {
        var log = new List<string>();
        var chart = RenderChart(Combine(NoAxes(), Recording("series", log)));

        using var surface = SKSurface.Create(new SKImageInfo(1, 1))!;
        await chart.InvokeAsync(() => typeof(NTChart<Pt>).GetMethod("OnPaintSurface", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic, [typeof(SKCanvas), typeof(SKImageInfo)])!.Invoke(chart.Instance, [surface.Canvas, new SKImageInfo(0, 0)]));

        log.Should().BeEmpty();
    }

    [Fact]
    public async Task BackgroundColor_FillsTheWholeCanvasWithTheResolvedThemeColor() {
        var chart = RenderChart(Combine(NoAxes(), Line()));

        using var surface = await Paint(chart);
        using var bitmap = Bitmap(surface);

        bitmap.GetPixel(0, 0).Should().Be(SKColors.White);
        bitmap.GetPixel(Width - 1, Height - 1).Should().Be(SKColors.White);
    }

    [Fact]
    public async Task BackgroundColorParameter_ChangesTheFilledColor() {
        var chart = RenderChart(Combine(NoAxes(), Line()), p => p.Add(c => c.BackgroundColor, NTColor.Secondary));

        using var surface = await Paint(chart);
        using var bitmap = Bitmap(surface);

        IsColor(bitmap.GetPixel(2, 2), new SKColor(0, 0, 255)).Should().BeTrue();
    }

    [Fact]
    public async Task XAxis_DrawsAnAxisLineInTheOutlineColorAtThePlotBottom() {
        ThemeColors["Outline"] = "#000000";
        var chart = RenderChart(Combine(XAxis<double>(p => p.X), YAxis(), Line()));

        using var surface = await Paint(chart);
        using var bitmap = Bitmap(surface);
        var plot = PlotArea(chart);

        InkAt(bitmap, (int)plot.MidX, (int)plot.Bottom, SKColors.Black).Should().BeGreaterThan(0.4f);
    }

    [Fact]
    public async Task YAxis_DrawsAnAxisLineAtThePlotLeft() {
        ThemeColors["Outline"] = "#000000";
        var chart = RenderChart(Combine(XAxis<double>(p => p.X), YAxis(), Line()));

        using var surface = await Paint(chart);
        using var bitmap = Bitmap(surface);
        var plot = PlotArea(chart);

        InkAt(bitmap, (int)plot.Left, (int)plot.MidY, SKColors.Black).Should().BeGreaterThan(0.4f);
    }

    [Fact]
    public async Task HiddenAxes_DrawNoAxisLines() {
        var chart = RenderChart(Combine(NoAxes(), Recording("series")));

        using var surface = await Paint(chart);
        using var bitmap = Bitmap(surface);

        CountColor(bitmap, SKColors.Black, 90).Should().Be(0);
    }

    [Fact]
    public async Task SecondaryYAxis_DrawsItsAxisLineOnThePlotRight() {
        ThemeColors["Outline"] = "#000000";
        var chart = RenderChart(Combine(XAxis<double>(p => p.X), YAxis(), YAxis(), Line(), Line(extra: Args(("UseSecondaryYAxis", true)))));

        using var surface = await Paint(chart);
        using var bitmap = Bitmap(surface);
        var plot = PlotArea(chart);

        InkAt(bitmap, (int)plot.Right, (int)plot.MidY, SKColors.Black).Should().BeGreaterThan(0.4f);
    }

    [Fact]
    public async Task Resize_RecomputesPlotAreaAndRedrawsAxesForTheNewSize() {
        ThemeColors["Outline"] = "#000000";
        var chart = RenderChart(Combine(XAxis<double>(p => p.X), YAxis(), Line()));
        using var small = await Paint(chart, 320, 200);
        var smallPlot = PlotArea(chart);

        using var large = await Paint(chart, 640, 400);
        using var bitmap = Bitmap(large);
        var largePlot = PlotArea(chart);

        largePlot.Width.Should().BeGreaterThan(smallPlot.Width * 1.8f);
        largePlot.Height.Should().BeGreaterThan(smallPlot.Height * 1.5f);
        InkAt(bitmap, (int)largePlot.MidX, (int)largePlot.Bottom, SKColors.Black).Should().BeGreaterThan(0.4f, "the cached axis picture must not be reused for a different surface size");
        InkAt(bitmap, (int)largePlot.Left, (int)largePlot.MidY, SKColors.Black).Should().BeGreaterThan(0.4f);
    }

    [Fact]
    public async Task DataChange_RebuildsAxesSoTheGutterFollowsTheNewLabels() {
        var chart = RenderChart(Combine(YAxis(), Line()));
        using var surface = await Paint(chart);
        var before = PlotArea(chart);

        chart.Render(p => p.Add(c => c.ChildContent, Combine(YAxis(), Line([new("A", 0, 1_000_000m), new("B", 1, 9_000_000m)]))));
        await Paint(chart, surface: surface);

        PlotArea(chart).Left.Should().BeGreaterThan(before.Left);
    }

    [Fact]
    public async Task ThemeChange_RedrawsAxesWithTheNewOutlineColor() {
        var chart = RenderChart(Combine(XAxis<double>(p => p.X), YAxis(), Line()));
        using var surface = await Paint(chart);
        ThemeColors["Outline"] = "#ff0000";

        await chart.InvokeAsync(chart.Instance.OnThemeChanged);
        await Paint(chart, surface: surface);
        using var bitmap = Bitmap(surface);
        var plot = PlotArea(chart);

        InkAt(bitmap, (int)plot.MidX, (int)plot.Bottom, new SKColor(255, 0, 0)).Should().BeGreaterThan(0.4f);
    }

    [Fact]
    public async Task Title_ConsumesThirtyTwoPixelsFromTheTop() {
        var withoutTitle = await PlotAreaOf(Combine(NoAxes(), Line()));
        var withTitle = await PlotAreaOf(Combine(NoAxes(), Line()), p => p.Add(c => c.TitleOptions, new NTTitleOptions("Revenue")));

        withTitle.Top.Should().BeApproximately(withoutTitle.Top + 32, Tolerance);
        withTitle.Bottom.Should().BeApproximately(withoutTitle.Bottom, Tolerance);
    }

    [Fact]
    public async Task Title_ReservedHeightScalesWithDensity() {
        var withTitle = await PlotAreaOf(Combine(NoAxes(), Line()), p => p.Add(c => c.TitleOptions, new NTTitleOptions("Revenue")), density: 2f);

        withTitle.Top.Should().BeApproximately((8 * 2f) + (32 * 2f), Tolerance);
    }

    [Fact]
    public async Task Title_IsDrawnCenteredInTheTitleBandWithTheOverrideColor() {
        var options = new NTTitleOptions("Revenue") { TextColor = NTColor.Secondary };
        var chart = RenderChart(Combine(NoAxes(), Line()), p => p.Add(c => c.TitleOptions, options));

        using var surface = await Paint(chart);
        using var bitmap = Bitmap(surface);
        var bounds = BoundsOf(bitmap, new SKColor(0, 0, 255), 40);

        bounds.Should().NotBeNull();
        bounds!.Value.Top.Should().BeGreaterThanOrEqualTo(8);
        bounds.Value.Bottom.Should().BeLessThan(8 + 32);
        ((bounds.Value.Top + bounds.Value.Bottom) / 2f).Should().BeApproximately(8 + 12, 3f, "the title is centered in its 24dp line");
        ((bounds.Value.Left + bounds.Value.Right) / 2f).Should().BeApproximately(Width / 2f, 3f);
    }

    [Fact]
    public async Task Title_LargerFontSizeProducesTallerText() {
        async Task<int> TitleHeight(float size) {
            var options = new NTTitleOptions("HEIGHT") { TextColor = NTColor.Secondary, FontSize = size };
            var chart = RenderChart(Combine(NoAxes(), Line()), p => p.Add(c => c.TitleOptions, options));
            using var surface = await Paint(chart);
            using var bitmap = Bitmap(surface);
            var bounds = BoundsOf(bitmap, new SKColor(0, 0, 255), 40)!.Value;
            return bounds.Bottom - bounds.Top;
        }

        var small = await TitleHeight(10);
        var large = await TitleHeight(24);

        large.Should().BeGreaterThan(small + 5);
    }

    [Fact]
    public async Task Title_UsesTheChartTextColorByDefault() {
        var chart = RenderChart(Combine(NoAxes(), Line()), p => p.Add(c => c.TitleOptions, new NTTitleOptions("Revenue")));

        using var surface = await Paint(chart);
        using var bitmap = Bitmap(surface);

        BoundsOf(bitmap, SKColors.Black, 40, new SKRectI(0, 0, Width, 40)).Should().NotBeNull();
    }

    [Fact]
    public async Task RemovingTitleOptions_ReleasesTheTitleBand() {
        var options = new NTTitleOptions("Revenue");
        var chart = RenderChart(Combine(NoAxes(), Line()), p => p.Add(c => c.TitleOptions, options));
        using var surface = await Paint(chart);
        PlotArea(chart).Top.Should().BeApproximately(40, Tolerance);

        chart.Render(p => p.Add(c => c.TitleOptions, (NTTitleOptions?)null));
        await Paint(chart, surface: surface);

        PlotArea(chart).Top.Should().BeApproximately(8, Tolerance);
        using var bitmap = Bitmap(surface);
        BoundsOf(bitmap, SKColors.Black, 40, new SKRectI(0, 0, Width, 40)).Should().BeNull("the title text must no longer be drawn");
    }

    [Fact]
    public void NTTitleOptions_WhitespaceTitle_Throws() {
        var act = () => new NTTitleOptions("   ");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void NTTitleOptions_Defaults_UseSixteenPointFontAndInheritedColor() {
        var options = new NTTitleOptions("Hello");

        options.Title.Should().Be("Hello");
        options.FontSize.Should().Be(16f);
        options.TextColor.Should().BeNull();
    }

    [Fact]
    public async Task Title_TextColorChangeIsAppliedOnTheNextFrame() {
        var options = new NTTitleOptions("Revenue") { TextColor = NTColor.Secondary };
        var chart = RenderChart(Combine(NoAxes(), Line()), p => p.Add(c => c.TitleOptions, options));
        using var surface = await Paint(chart);

        chart.Render(p => p.Add(c => c.TitleOptions, new NTTitleOptions("Revenue") { TextColor = NTColor.Tertiary }));
        await Paint(chart, surface: surface);
        using var bitmap = Bitmap(surface);

        BoundsOf(bitmap, new SKColor(0, 170, 0), 40, new SKRectI(0, 0, Width, 40)).Should().NotBeNull();
        BoundsOf(bitmap, new SKColor(0, 0, 255), 40, new SKRectI(0, 0, Width, 40)).Should().BeNull();
    }
}
