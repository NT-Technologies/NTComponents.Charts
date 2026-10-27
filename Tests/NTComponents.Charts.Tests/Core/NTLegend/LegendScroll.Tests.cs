using Microsoft.AspNetCore.Components;
using NTComponents.Charts.Core;
using SkiaSharp;

namespace NTComponents.Charts.Tests.ChartCore;

/// <summary>
///     A legend with more items than fit keeps a bounded size and scrolls its items under the mouse wheel instead of
///     shrinking the plot or running off the canvas.
/// </summary>
public class NTLegend_Scroll_Tests : ChartTestBase {
    private const float Tolerance = 0.01f;

    private static RenderFragment NoAxes() => Combine(XAxis<double>(p => p.X, Args(("Visible", false))), YAxis(Args(("Visible", false))));

    private static RenderFragment Items(int count) =>
        Combine([.. Enumerable.Range(0, count).Select(i => Recording($"s{i}", extra: Args(("Title", $"Body part {i}"))))]);

    private async Task<(IRenderedComponent<NTChart<Pt>> Chart, NTLegend<Pt> Legend, SKSurface Surface)> Paint(LegendPosition position, int count, params (string Name, object Value)[] legend) {
        var chart = RenderChart(Combine(NoAxes(), Legend(Args([("Position", position), .. legend])), Items(count)));
        var surface = await Paint(chart);
        return (chart, chart.FindComponent<NTLegend<Pt>>().Instance, surface);
    }

    // Labels of the items that can be hit anywhere inside the legend's scroll window.
    private static HashSet<string> VisibleItems(NTLegend<Pt> legend) {
        var labels = new HashSet<string>();
        var area = legend.LastDrawArea;
        for (var y = area.Top; y < area.Bottom; y += 4) {
            for (var x = area.Left; x < area.Right; x += 4) {
                if (legend.GetItemAtPoint(new SKPoint(x, y)) is { } item) {
                    labels.Add(item.Label);
                }
            }
        }
        return labels;
    }

    private static Task Wheel(IRenderedComponent<NTChart<Pt>> chart, SKPoint point, double deltaY, double deltaX = 0) =>
        chart.InvokeAsync(() => chart.Instance.OnNativeWheel(new NTChart<Pt>.NTNativeWheelEventArgs { OffsetX = point.X, OffsetY = point.Y, DeltaX = deltaX, DeltaY = deltaY }));

    [Fact]
    public async Task ManyItemsAtTheBottom_ReserveOnlyTwoRows() {
        var (chart, legend, surface) = await Paint(LegendPosition.Bottom, 40);
        using var _ = surface;

        // Two 24px rows plus 8px of padding, however many rows the items need.
        PlotArea(chart).Bottom.Should().BeApproximately(Height - 8 - 56, Tolerance);
        legend.ScrollRegion.Should().Be(legend.LastDrawArea);
    }

    [Fact]
    public async Task MaxRows_SetsHowManyRowsATopLegendShows() {
        var (chart, _, surface) = await Paint(LegendPosition.Top, 40, ("MaxRows", 3));
        using var _ = surface;

        PlotArea(chart).Top.Should().BeApproximately(8 + (3 * 24) + 8, Tolerance);
    }

    [Theory]
    [InlineData(LegendPosition.Top)]
    [InlineData(LegendPosition.Bottom)]
    public async Task OverflowingTopOrBottomLegend_FillsItsRowsColumnByColumn(LegendPosition position) {
        var (_, legend, surface) = await Paint(position, 40);
        using var _ = surface;
        var firstItem = new SKPoint(legend.LastDrawArea.Left + 4 + 10, legend.LastDrawArea.Top + 4 + 12);

        legend.GetItemAtPoint(firstItem)!.Label.Should().Be("Body part 0");
        legend.GetItemAtPoint(new SKPoint(firstItem.X, firstItem.Y + 24))!.Label.Should().Be("Body part 1", "the second item starts the second row of the first column");
    }

    [Fact]
    public async Task HorizontalWheel_ScrollsABottomLegendSideways() {
        var (chart, legend, surface) = await Paint(LegendPosition.Bottom, 40);
        var center = new SKPoint(legend.LastDrawArea.MidX, legend.LastDrawArea.MidY);

        await Wheel(chart, center, 0, deltaX: 100_000);
        await Paint(chart, surface: surface);
        surface.Dispose();

        VisibleItems(legend).Should().Contain("Body part 39").And.NotContain("Body part 0");
    }

    [Fact]
    public async Task HorizontalWheel_LeavesASideLegendInPlace() {
        var (chart, legend, surface) = await Paint(LegendPosition.Right, 40);
        var center = new SKPoint(legend.LastDrawArea.MidX, legend.LastDrawArea.MidY);

        await Wheel(chart, center, 0, deltaX: 100_000);
        await Paint(chart, surface: surface);
        surface.Dispose();

        VisibleItems(legend).Should().Contain("Body part 0").And.NotContain("Body part 39");
    }

    [Fact]
    public async Task OverflowingBottomLegend_DrawsItsScrollThumbAlongTheBottom() {
        var (_, legend, surface) = await Paint(LegendPosition.Bottom, 40);
        using var _ = surface;
        using var bitmap = Bitmap(surface);
        var area = SKRectI.Round(legend.LastDrawArea);
        var y = area.Bottom - 2;
        var background = bitmap.GetPixel(area.Right - 1, y);

        var thumb = Enumerable.Range(area.Left, area.Width).Where(x => bitmap.GetPixel(x, y) != background).ToList();

        thumb.Should().NotBeEmpty();
        thumb[0].Should().BeLessThan(area.Left + 4, "the view starts at the first column");
        thumb.Should().HaveCountLessThan(area.Width / 2);
    }

    [Fact]
    public async Task ItemsThatFit_DoNotScroll() {
        var (chart, legend, surface) = await Paint(LegendPosition.Bottom, 3);
        using var _ = surface;

        legend.ScrollRegion.Should().Be(SKRect.Empty);
        VisibleItems(legend).Should().BeEquivalentTo("Body part 0", "Body part 1", "Body part 2");
    }

    [Fact]
    public async Task ManyItemsOnTheRight_StayInsideTheCanvas() {
        var (_, legend, surface) = await Paint(LegendPosition.Right, 40);
        using var _ = surface;
        using var bitmap = Bitmap(surface);

        legend.ScrollRegion.Should().NotBe(SKRect.Empty);
        var area = SKRectI.Round(legend.LastDrawArea);
        BoundsOf(bitmap, SKColor.Parse("#ff0000"), 10, new SKRectI(area.Left, Height - 8, area.Right, Height)).Should().BeNull("items past the legend's padding are clipped");
        VisibleItems(legend).Should().Contain("Body part 0").And.NotContain("Body part 39");
    }

    [Theory]
    [InlineData(LegendPosition.Bottom)]
    [InlineData(LegendPosition.Right)]
    [InlineData(LegendPosition.Floating)]
    public async Task WheelOverAnOverflowingLegend_ScrollsToTheLastItemAndStops(LegendPosition position) {
        var (chart, legend, surface) = await Paint(position, 40);
        var center = new SKPoint(legend.LastDrawArea.MidX, legend.LastDrawArea.MidY);

        await Wheel(chart, center, 100_000);
        await Paint(chart, surface: surface);
        surface.Dispose();

        VisibleItems(legend).Should().Contain("Body part 39").And.NotContain("Body part 0");
    }

    [Fact]
    public async Task WheelBackUp_ReturnsToTheFirstItem() {
        var (chart, legend, surface) = await Paint(LegendPosition.Bottom, 40);
        var center = new SKPoint(legend.LastDrawArea.MidX, legend.LastDrawArea.MidY);
        await Wheel(chart, center, 100_000);
        await Paint(chart, surface: surface);

        await Wheel(chart, center, -100_000);
        await Paint(chart, surface: surface);
        surface.Dispose();

        VisibleItems(legend).Should().Contain("Body part 0").And.NotContain("Body part 39");
    }

    [Fact]
    public async Task WheelOverTheLegend_ScrollsByTheWheelDistance() {
        var (chart, legend, surface) = await Paint(LegendPosition.Right, 40);
        var firstRow = new SKPoint(legend.LastDrawArea.Left + 20, legend.LastDrawArea.Top + 8 + 12);
        legend.GetItemAtPoint(firstRow)!.Label.Should().Be("Body part 0");

        await Wheel(chart, firstRow, 48);
        await Paint(chart, surface: surface);
        surface.Dispose();

        legend.GetItemAtPoint(firstRow)!.Label.Should().Be("Body part 2", "a 48px wheel moves two 24px rows");
    }

    [Fact]
    public async Task WheelOutsideTheLegend_LeavesItsItemsInPlace() {
        var (chart, legend, surface) = await Paint(LegendPosition.Bottom, 40);
        var before = VisibleItems(legend);

        await Wheel(chart, new SKPoint(Width / 2f, 40), 100_000);
        await Paint(chart, surface: surface);
        surface.Dispose();

        VisibleItems(legend).Should().BeEquivalentTo(before);
    }

    [Fact]
    public async Task OverflowingLegend_SendsItsScrollRegionToTheBrowserOnce() {
        var listeners = Module.SetupModule("initializeChart", _ => true);
        listeners.SetupVoid("setScrollRegion", _ => true).SetVoidResult();
        var (chart, legend, surface) = await Paint(LegendPosition.Bottom, 40);

        await Paint(chart, surface: surface);
        surface.Dispose();

        var calls = listeners.Invocations.Where(i => i.Identifier == "setScrollRegion").ToList();
        calls.Should().ContainSingle("an unchanged region is not sent again");
        var region = legend.ScrollRegion;
        calls[0].Arguments.Single().Should().BeEquivalentTo(new[] { region.Left, region.Top, region.Right, region.Bottom });
    }
}
