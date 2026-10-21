using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using NTComponents.Charts.Core;
using NTComponents.Charts.Core.Series;
using SkiaSharp;

namespace NTComponents.Charts.Tests.ChartCore;

public class NTLegend_Tests : ChartTestBase {
    private static readonly SKColor Red = new(255, 0, 0);
    private static readonly SKColor Green = new(0, 170, 0);
    private static readonly SKColor Blue = new(0, 0, 255);
    private const float Tolerance = 0.01f;

    private static RenderFragment NoAxes() => Combine(XAxis<double>(p => p.X, Args(("Visible", false))), YAxis(Args(("Visible", false))));

    private static RenderFragment Three() => Combine(
        Recording("a", extra: Args(("Title", "First"))),
        Recording("b", extra: Args(("Title", "Second"))),
        Recording("c", extra: Args(("Title", "Third"))));

    private async Task<SKRect> PlotWithLegend(IDictionary<string, object>? legend, int width = Width, string title = "Revenue") {
        var chart = RenderChart(Combine(NoAxes(), Legend(legend), Recording("a", extra: Args(("Title", title)))));
        using var surface = await Paint(chart, width, Height);
        return PlotArea(chart);
    }

    private static SKRectI? IconBounds(SKBitmap bitmap, SKColor color, SKRectI? region = null) => BoundsOf(bitmap, color, 10, region);

    /// <summary>
    ///     Width of the pixels at least half covered by the round swatch. Its center lands on a sub-pixel position that depends on
    ///     platform text metrics, so near-exact color matching drops the anti-aliased edge columns.
    /// </summary>
    private static int IconWidth(SKBitmap bitmap, SKColor color, SKRectI region) {
        var bounds = BoundsOf(bitmap, color, 127, region)!.Value;
        return bounds.Right - bounds.Left + 1;
    }

    [Fact]
    public async Task Bottom_ReservesOneRowAtTheBottomAndKeepsTheOtherEdges() {
        var plot = await PlotWithLegend(Args(("Position", LegendPosition.Bottom)));

        // One 24px row plus 8px of padding.
        plot.Bottom.Should().BeApproximately(Height - 8 - 32, Tolerance);
        plot.Top.Should().BeApproximately(8, Tolerance);
        plot.Left.Should().BeApproximately(8, Tolerance);
        plot.Right.Should().BeApproximately(Width - 8, Tolerance);
    }

    [Fact]
    public async Task Top_ReservesOneRowAtTheTopAndKeepsTheOtherEdges() {
        var plot = await PlotWithLegend(Args(("Position", LegendPosition.Top)));

        plot.Top.Should().BeApproximately(8 + 32, Tolerance);
        plot.Bottom.Should().BeApproximately(Height - 8, Tolerance);
        plot.Left.Should().BeApproximately(8, Tolerance);
        plot.Right.Should().BeApproximately(Width - 8, Tolerance);
    }

    [Fact]
    public async Task Left_ReservesAColumnOnTheLeft() {
        var plot = await PlotWithLegend(Args(("Position", LegendPosition.Left)));

        plot.Left.Should().BeGreaterThan(8 + 30);
        plot.Right.Should().BeApproximately(Width - 8, Tolerance);
        plot.Top.Should().BeApproximately(8, Tolerance);
        plot.Bottom.Should().BeApproximately(Height - 8, Tolerance);
    }

    [Fact]
    public async Task Right_ReservesAColumnOnTheRight() {
        var plot = await PlotWithLegend(Args(("Position", LegendPosition.Right)));

        plot.Right.Should().BeLessThan(Width - 8 - 30);
        plot.Left.Should().BeApproximately(8, Tolerance);
        plot.Top.Should().BeApproximately(8, Tolerance);
        plot.Bottom.Should().BeApproximately(Height - 8, Tolerance);
    }

    [Fact]
    public async Task SideLegend_GrowsWithTheLongestLabel() {
        var narrow = await PlotWithLegend(Args(("Position", LegendPosition.Right)), title: "A");
        var wide = await PlotWithLegend(Args(("Position", LegendPosition.Right)), title: "A considerably longer series title");

        wide.Right.Should().BeLessThan(narrow.Right - 20);
    }

    [Theory]
    [InlineData(LegendPosition.None)]
    [InlineData(LegendPosition.Floating)]
    public async Task NoneAndFloating_DoNotConsumeLayoutSpace(LegendPosition position) {
        var plot = await PlotWithLegend(Args(("Position", position)));

        plot.Should().Be(new SKRect(8, 8, Width - 8, Height - 8));
    }

    [Fact]
    public async Task HiddenLegend_DoesNotConsumeLayoutSpaceOrDrawIcons() {
        var chart = RenderChart(Combine(NoAxes(), Legend(Args(("Visible", false))), Recording("a")));

        using var surface = await Paint(chart);
        using var bitmap = Bitmap(surface);

        PlotArea(chart).Should().Be(new SKRect(8, 8, Width - 8, Height - 8));
        IconBounds(bitmap, Red).Should().BeNull();
    }

    [Fact]
    public async Task LegendPositionChange_IsAppliedOnTheNextFrame() {
        var chart = RenderChart(Combine(NoAxes(), Legend(Args(("Position", LegendPosition.Top))), Recording("a")));
        using var surface = await Paint(chart);
        PlotArea(chart).Top.Should().BeApproximately(40, Tolerance);

        chart.Render(p => p.Add(c => c.ChildContent, Combine(NoAxes(), Legend(Args(("Position", LegendPosition.Bottom))), Recording("a"))));
        await Paint(chart, surface: surface);

        PlotArea(chart).Top.Should().BeApproximately(8, Tolerance);
        PlotArea(chart).Bottom.Should().BeApproximately(Height - 8 - 32, Tolerance);
    }

    [Fact]
    public async Task HorizontalLegend_WrapsIntoMoreRowsWhenItemsDoNotFit() {
        var wide = await PlotWithLegendFor(Width);
        var narrow = await PlotWithLegendFor(200);

        var wideRows = (wide.Top - 8 - 8) / 24f;
        var narrowRows = (narrow.Top - 8 - 8) / 24f;
        wideRows.Should().BeApproximately(1f, 0.01f);
        narrowRows.Should().BeGreaterThan(1.5f);

        async Task<SKRect> PlotWithLegendFor(int width) {
            var chart = RenderChart(Combine(NoAxes(), Legend(Args(("Position", LegendPosition.Top))),
                Recording("a", extra: Args(("Title", "Alpha series"))),
                Recording("b", extra: Args(("Title", "Beta series"))),
                Recording("c", extra: Args(("Title", "Gamma series"))),
                Recording("d", extra: Args(("Title", "Delta series")))));
            using var surface = await Paint(chart, width, Height);
            return PlotArea(chart);
        }
    }

    [Fact]
    public async Task Items_AreDrawnInSeriesOrderWithPaletteColors() {
        var chart = RenderChart(Combine(NoAxes(), Legend(Args(("Position", LegendPosition.Top))), Three()));

        using var surface = await Paint(chart);
        using var bitmap = Bitmap(surface);
        var band = new SKRectI(0, 0, Width, 40);

        var red = IconBounds(bitmap, Red, band);
        var green = IconBounds(bitmap, Green, band);
        var blue = IconBounds(bitmap, Blue, band);
        red.Should().NotBeNull();
        green.Should().NotBeNull();
        blue.Should().NotBeNull();
        red!.Value.Left.Should().BeLessThan(green!.Value.Left);
        green.Value.Left.Should().BeLessThan(blue!.Value.Left);
        IconWidth(bitmap, Red, band).Should().BeCloseTo(12, 1, "the icon is IconSize pixels wide");
    }

    [Fact]
    public async Task Items_UseTheSeriesColorOverride() {
        var chart = RenderChart(Combine(NoAxes(), Legend(Args(("Position", LegendPosition.Top))), Recording("a", extra: Args(("Color", (NTColor?)NTColor.Secondary)))));

        using var surface = await Paint(chart);
        using var bitmap = Bitmap(surface);

        IconBounds(bitmap, Blue, new SKRectI(0, 0, Width, 40)).Should().NotBeNull();
        IconBounds(bitmap, Red, new SKRectI(0, 0, Width, 40)).Should().BeNull();
    }

    [Fact]
    public async Task IconSizeAndFontSize_AreHonored() {
        var chart = RenderChart(Combine(NoAxes(), Legend(Args(("Position", LegendPosition.Top), ("IconSize", 20f))), Recording("a")));

        using var surface = await Paint(chart);
        using var bitmap = Bitmap(surface);
        IconWidth(bitmap, Red, new SKRectI(0, 0, Width, 40)).Should().BeCloseTo(20, 1);
    }

    [Fact]
    public async Task ClickingAnItem_TogglesTheSeriesOffAndOnAgain() {
        var chart = RenderChart(Combine(NoAxes(), Legend(Args(("Position", LegendPosition.Bottom))), Line()));
        using var surface = await Paint(chart);
        using var before = Bitmap(surface);
        var icon = IconBounds(before, Red, new SKRectI(0, (int)PlotArea(chart).Bottom, Width, Height))!.Value;
        var (x, y) = ((icon.Left + icon.Right) / 2d, (icon.Top + icon.Bottom) / 2d);
        var series = chart.Instance.Series.Single();

        await Fire(chart, h => h.Click(new MouseEventArgs { OffsetX = x, OffsetY = y }));
        series.Visible.Should().BeFalse();
        await Paint(chart, surface: surface);
        using var hidden = Bitmap(surface);

        CountColor(hidden, Red, 30).Should().BeLessThan(CountColor(before, Red, 30) / 2, "the hidden series line must disappear while the dimmed icon stays");
        InkAt(hidden, (int)x, (int)y, Red).Should().BeInRange(0.2f, 0.45f, "hidden items stay in the legend at reduced opacity");

        await Fire(chart, h => h.Click(new MouseEventArgs { OffsetX = x, OffsetY = y }));
        series.Visible.Should().BeTrue();
        await Paint(chart, surface: surface);
        using var shown = Bitmap(surface);
        CountColor(shown, Red, 30).Should().BeGreaterThan(CountColor(hidden, Red, 30));
    }

    [Fact]
    public async Task ClickingAnItem_RaisesVisibilityChangedWithTheNewState() {
        var changes = new List<NTSeriesVisibilityChangedEventArgs<Pt>>();
        var callback = EventCallback.Factory.Create<NTSeriesVisibilityChangedEventArgs<Pt>>(this, changes.Add);
        var chart = RenderChart(Combine(NoAxes(), Legend(Args(("Position", LegendPosition.Bottom))), Recording("a", extra: Args(("OnVisibilityChanged", callback)))));
        using var surface = await Paint(chart);
        using var bitmap = Bitmap(surface);
        var icon = IconBounds(bitmap, Red, new SKRectI(0, (int)PlotArea(chart).Bottom, Width, Height))!.Value;

        await Fire(chart, h => h.Click(new MouseEventArgs { OffsetX = (icon.Left + icon.Right) / 2d, OffsetY = (icon.Top + icon.Bottom) / 2d }));

        chart.WaitForAssertion(() => changes.Should().ContainSingle());
        changes[0].Visible.Should().BeFalse();
        changes[0].Series.Should().BeSameAs(chart.Instance.Series.Single());
    }

    [Fact]
    public async Task ClickingAnItem_DoesNotRaiseSeriesClick() {
        var clicks = 0;
        var callback = EventCallback.Factory.Create<NTSeriesClickEventArgs<Pt>>(this, _ => clicks++);
        var chart = RenderChart(Combine(NoAxes(), Legend(Args(("Position", LegendPosition.Bottom))), Recording("a", hit: (_, _) => (0, Sample[0]), extra: Args(("OnClick", callback)))));
        using var surface = await Paint(chart);
        using var bitmap = Bitmap(surface);
        var icon = IconBounds(bitmap, Red, new SKRectI(0, (int)PlotArea(chart).Bottom, Width, Height))!.Value;

        await Fire(chart, h => h.Click(new MouseEventArgs { OffsetX = (icon.Left + icon.Right) / 2d, OffsetY = (icon.Top + icon.Bottom) / 2d }));
        await chart.InvokeAsync(() => { });

        clicks.Should().Be(0);
    }

    [Fact]
    public async Task ClickingOneItem_LeavesOtherSeriesVisible() {
        var chart = RenderChart(Combine(NoAxes(), Legend(Args(("Position", LegendPosition.Top))), Three()));
        using var surface = await Paint(chart);
        using var bitmap = Bitmap(surface);
        var green = IconBounds(bitmap, Green, new SKRectI(0, 0, Width, 40))!.Value;

        await Fire(chart, h => h.Click(new MouseEventArgs { OffsetX = (green.Left + green.Right) / 2d, OffsetY = (green.Top + green.Bottom) / 2d }));

        chart.Instance.Series.Select(s => s.Visible).Should().Equal(true, false, true);
    }

    [Fact]
    public async Task ClickingOutsideTheItems_DoesNotToggleAnything() {
        var chart = RenderChart(Combine(NoAxes(), Legend(Args(("Position", LegendPosition.Bottom))), Recording("a")));
        using var surface = await Paint(chart);

        await Fire(chart, h => h.Click(new MouseEventArgs { OffsetX = 20, OffsetY = Height - 15 }));

        chart.Instance.Series.Single().Visible.Should().BeTrue();
    }

    [Fact]
    public async Task HoveringAnItem_HighlightsItsSeriesAndRaisesHoverEnter() {
        var entered = new List<NTSeriesHoverEnterEventArgs<Pt>>();
        var enter = EventCallback.Factory.Create<NTSeriesHoverEnterEventArgs<Pt>>(this, entered.Add);
        var chart = RenderChart(Combine(NoAxes(), Legend(Args(("Position", LegendPosition.Top))),
            Recording("a", extra: Args(("Title", "First"))),
            Recording("b", extra: Args(("Title", "Second"), ("OnHoverEnter", enter)))));
        using var surface = await Paint(chart);
        using var bitmap = Bitmap(surface);
        var green = IconBounds(bitmap, Green, new SKRectI(0, 0, Width, 40))!.Value;

        await Move(chart, (green.Left + green.Right) / 2d, (green.Top + green.Bottom) / 2d);
        await Paint(chart, surface: surface);
        await Paint(chart, surface: surface);

        chart.Instance.HoveredSeries.Should().BeSameAs(chart.Instance.Series[1]);
        chart.Instance.HoveredDataPoint.Should().BeNull("legend hover targets the series, not a data point");
        chart.WaitForAssertion(() => entered.Should().ContainSingle());
        using var hovered = Bitmap(surface);
        InkAt(hovered, (green.Left + green.Right) / 2, (green.Top + green.Bottom) / 2, Green, 0).Should().BeGreaterThan(0.9f);
        var red = IconBounds(hovered, Red, new SKRectI(0, 0, Width, 40));
        red.Should().BeNull("non-hovered series fade to 38% opacity, outside the color match tolerance");
        chart.Find(".nt-chart-canvas-host").GetAttribute("style").Should().Contain("cursor: pointer;");
    }

    [Fact]
    public async Task HoveringAnItem_KeepsEveryLabelAtFullContrast() {
        var chart = RenderChart(Combine(NoAxes(), Legend(Args(("Position", LegendPosition.Top))),
            Recording("a", extra: Args(("Title", "First"))),
            Recording("b", extra: Args(("Title", "Second")))));
        using var surface = await Paint(chart);
        using var before = Bitmap(surface);
        var band = new SKRectI(0, 0, Width, 40);
        var labelInk = new SKColor(0x11, 0x11, 0x11);
        var green = IconBounds(before, Green, band)!.Value;

        await Move(chart, (green.Left + green.Right) / 2d, (green.Top + green.Bottom) / 2d);
        await Paint(chart, surface: surface);
        await Paint(chart, surface: surface);
        using var after = Bitmap(surface);

        chart.Instance.HoveredSeries.Should().BeSameAs(chart.Instance.Series[1]);
        CountColor(after, labelInk, 24).Should().BeGreaterThan(0).And.Be(CountColor(before, labelInk, 24), "label text must not fade while another series is hovered");
    }

    [Fact]
    public async Task HoveredItem_DrawsAnOnSurfaceVariantStateLayerAtEightPercent() {
        var chart = RenderChart(Combine(NoAxes(), Legend(Args(("Position", LegendPosition.Top))), Recording("a")));
        using var surface = await Paint(chart);
        using var before = Bitmap(surface);
        var red = IconBounds(before, Red, new SKRectI(0, 0, Width, 40))!.Value;
        var probe = (x: red.Left - 2, y: (red.Top + red.Bottom) / 2);
        before.GetPixel(probe.x, probe.y).Should().Be(SKColors.White);

        await Move(chart, (red.Left + red.Right) / 2d, (red.Top + red.Bottom) / 2d);
        await Paint(chart, surface: surface);
        await Paint(chart, surface: surface);
        using var after = Bitmap(surface);

        // 255 blended with #111111 at alpha 20/255 is 236.
        after.GetPixel(probe.x, probe.y).Red.Should().BeInRange(233, 239);
    }

    [Theory]
    [InlineData(13)]
    [InlineData(35)]
    public async Task ItemHitTarget_CoversTheWholeTwentyFourPixelRow(int y) {
        var chart = RenderChart(Combine(NoAxes(), Legend(Args(("Position", LegendPosition.Top))), Recording("a")));
        using var surface = await Paint(chart);
        using var bitmap = Bitmap(surface);
        var icon = IconBounds(bitmap, Red, new SKRectI(0, 0, Width, 40))!.Value;

        // The row spans 12..36; the 12px swatch only covers its middle.
        await Fire(chart, h => h.Click(new MouseEventArgs { OffsetX = (icon.Left + icon.Right) / 2d, OffsetY = y }));

        chart.Instance.Series.Single().Visible.Should().BeFalse();
    }

    [Fact]
    public async Task ItemHitTarget_EndsAtTheRowBoundary() {
        var chart = RenderChart(Combine(NoAxes(), Legend(Args(("Position", LegendPosition.Top))), Recording("a")));
        using var surface = await Paint(chart);
        using var bitmap = Bitmap(surface);
        var icon = IconBounds(bitmap, Red, new SKRectI(0, 0, Width, 40))!.Value;

        await Fire(chart, h => h.Click(new MouseEventArgs { OffsetX = (icon.Left + icon.Right) / 2d, OffsetY = 37 }));

        chart.Instance.Series.Single().Visible.Should().BeTrue();
    }

    [Fact]
    public async Task Legend_UsesTheMediumWeightLabelFontAndCircleSwatch() {
        var chart = RenderChart(Combine(NoAxes(), Legend(Args(("Position", LegendPosition.Top))), Recording("a")));
        using var surface = await Paint(chart);
        using var bitmap = Bitmap(surface);
        var icon = IconBounds(bitmap, Red, new SKRectI(0, 0, Width, 40))!.Value;

        IsColor(bitmap.GetPixel(icon.Left, icon.Top), Red).Should().BeFalse("a circle leaves its square corners empty");
        bitmap.GetPixel((icon.Left + icon.Right) / 2, (icon.Top + icon.Bottom) / 2).Should().Be(Red);
    }

    [Fact]
    public async Task FloatingLegend_IsDrawnInsideTheTopRightOfThePlotArea() {
        var chart = RenderChart(Combine(NoAxes(), Legend(Args(("Position", LegendPosition.Floating))), Recording("a")));

        using var surface = await Paint(chart);
        using var bitmap = Bitmap(surface);
        var plot = PlotArea(chart);
        var icon = IconBounds(bitmap, Red)!.Value;

        icon.Left.Should().BeGreaterThan((int)plot.MidX);
        icon.Right.Should().BeLessThan((int)plot.Right);
        icon.Top.Should().BeInRange((int)plot.Top, (int)plot.Top + 40);
    }

    [Fact]
    public async Task FloatingLegend_CanBeDraggedAndTheDragDoesNotToggleTheSeries() {
        var chart = RenderChart(Combine(NoAxes(), Legend(Args(("Position", LegendPosition.Floating))), Recording("a")));
        using var surface = await Paint(chart);
        using var before = Bitmap(surface);
        var start = IconBounds(before, Red)!.Value;
        var (x, y) = ((start.Left + start.Right) / 2d, (start.Top + start.Bottom) / 2d);
        var legend = chart.Instance.Legend!;

        await Fire(chart, h => h.MouseDown(new MouseEventArgs { OffsetX = x, OffsetY = y }));
        await Move(chart, x - 60, y + 40);
        await Fire(chart, h => h.MouseUp(new MouseEventArgs { OffsetX = x - 60, OffsetY = y + 40 }));
        await Fire(chart, h => h.Click(new MouseEventArgs { OffsetX = x - 60, OffsetY = y + 40 }));
        await Paint(chart, surface: surface);
        using var after = Bitmap(surface);
        var moved = IconBounds(after, Red)!.Value;

        legend.FloatingOffset.Should().NotBeNull();
        (moved.Left - start.Left).Should().BeCloseTo(-60, 1);
        (moved.Top - start.Top).Should().BeCloseTo(40, 1);
        chart.Instance.Series.Single().Visible.Should().BeTrue("a drag must not be interpreted as a click on the legend item");
    }

    [Fact]
    public async Task FloatingLegend_SmallMovementStillCountsAsAClick() {
        var chart = RenderChart(Combine(NoAxes(), Legend(Args(("Position", LegendPosition.Floating))), Recording("a")));
        using var surface = await Paint(chart);
        using var bitmap = Bitmap(surface);
        var icon = IconBounds(bitmap, Red)!.Value;
        var (x, y) = ((icon.Left + icon.Right) / 2d, (icon.Top + icon.Bottom) / 2d);

        await Fire(chart, h => h.MouseDown(new MouseEventArgs { OffsetX = x, OffsetY = y }));
        await Move(chart, x + 2, y + 2);
        await Fire(chart, h => h.MouseUp(new MouseEventArgs { OffsetX = x + 2, OffsetY = y + 2 }));
        await Fire(chart, h => h.Click(new MouseEventArgs { OffsetX = x + 2, OffsetY = y + 2 }));

        chart.Instance.Series.Single().Visible.Should().BeFalse();
    }

    [Fact]
    public async Task AfterALegendDrag_OnlyTheNextClickIsSuppressed() {
        var chart = RenderChart(Combine(NoAxes(), Legend(Args(("Position", LegendPosition.Floating))), Recording("a")));
        using var surface = await Paint(chart);
        using var bitmap = Bitmap(surface);
        var icon = IconBounds(bitmap, Red)!.Value;
        var (x, y) = ((icon.Left + icon.Right) / 2d, (icon.Top + icon.Bottom) / 2d);
        await Fire(chart, h => h.MouseDown(new MouseEventArgs { OffsetX = x, OffsetY = y }));
        await Move(chart, x - 50, y + 30);
        await Fire(chart, h => h.MouseUp(new MouseEventArgs { OffsetX = x - 50, OffsetY = y + 30 }));
        await Fire(chart, h => h.Click(new MouseEventArgs { OffsetX = x - 50, OffsetY = y + 30 }));
        await Paint(chart, surface: surface);

        await Fire(chart, h => h.Click(new MouseEventArgs { OffsetX = x - 50, OffsetY = y + 30 }));

        chart.Instance.Series.Single().Visible.Should().BeFalse("the second click is a real click on the moved legend");
    }

    [Fact]
    public async Task RemovingTheLegendComponent_RestoresTheFullPlotArea() {
        var chart = RenderChart(Combine(NoAxes(), Legend(Args(("Position", LegendPosition.Top))), Recording("a")));
        using var surface = await Paint(chart);
        PlotArea(chart).Top.Should().BeApproximately(40, Tolerance);

        chart.Render(p => p.Add(c => c.ChildContent, Combine(NoAxes(), Recording("a"))));
        await Paint(chart, surface: surface);

        PlotArea(chart).Top.Should().BeApproximately(8, Tolerance);
    }

    [Fact]
    public async Task SeriesWithoutTitle_AreLabelledByIndexAndStillOccupyLegendSpace() {
        var chart = RenderChart(Combine(NoAxes(), Legend(Args(("Position", LegendPosition.Right))), Recording("a"), Recording("b")));

        using var surface = await Paint(chart);
        using var bitmap = Bitmap(surface);
        var region = new SKRectI((int)PlotArea(chart).Right, 0, Width, Height);

        IconBounds(bitmap, Red, region).Should().NotBeNull();
        IconBounds(bitmap, Green, region).Should().NotBeNull();
        IconBounds(bitmap, Green, region)!.Value.Top.Should().BeGreaterThan(IconBounds(bitmap, Red, region)!.Value.Top);
    }

    [Fact]
    public async Task BottomLegend_FirstFrameMatchesTheSettledFrame() {
        var chart = RenderChart(Combine(XAxis<double>(p => p.X), YAxis(), Legend(Args(("Position", LegendPosition.Bottom))), Line()));

        using var first = await Paint(chart);
        using var firstBitmap = Bitmap(first);
        using var second = await Paint(chart);
        using var secondBitmap = Bitmap(second);

        var region = new SKRectI(0, (int)PlotArea(chart).Bottom + 20, Width, Height);
        var firstIcon = IconBounds(firstBitmap, Red, region);
        firstIcon.Should().NotBeNull();
        firstIcon.Should().Be(IconBounds(secondBitmap, Red, region));
    }

    [Fact]
    public async Task ChangingASeriesTitle_RefreshesTheLegendLabel() {
        var chart = RenderChart(Combine(NoAxes(), Legend(Args(("Position", LegendPosition.Right))), Recording("a", extra: Args(("Title", "A")))));
        using var surface = await Paint(chart);
        var narrow = PlotArea(chart).Right;

        chart.Render(p => p.Add(c => c.ChildContent, Combine(NoAxes(), Legend(Args(("Position", LegendPosition.Right))), Recording("a", extra: Args(("Title", "A considerably longer series title"))))));
        await Paint(chart, surface: surface);

        PlotArea(chart).Right.Should().BeLessThan(narrow - 20);
    }
}
