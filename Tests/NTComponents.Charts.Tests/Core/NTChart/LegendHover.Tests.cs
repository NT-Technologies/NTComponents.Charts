using Microsoft.AspNetCore.Components;
using NTComponents.Charts.Core;
using SkiaSharp;

namespace NTComponents.Charts.Tests.ChartCore;

public class NTChart_LegendHover_Tests : ChartTestBase {
    private static readonly SKColor Green = new(0, 170, 0);

    private static RenderFragment NoAxes() => Combine(XAxis<double>(p => p.X, Args(("Visible", false))), YAxis(Args(("Visible", false))));

    private async Task<(IRenderedComponent<NTChart<Pt>> Chart, SKSurface Surface, double X, double Y)> TwoSeriesWithSecondHidden(bool hide) {
        var chart = RenderChart(Combine(NoAxes(), Legend(Args(("Position", LegendPosition.Top))), Recording("a", extra: Args(("Title", "In store"))), Recording("b", extra: Args(("Title", "Online")))));
        var surface = await Paint(chart);
        using var bitmap = Bitmap(surface);
        var icon = BoundsOf(bitmap, Green, 10, new SKRectI(0, 0, Width, 40))!.Value;
        if (hide) {
            chart.Instance.Series[1].ToggleLegendItem((int?)null);
            await Paint(chart, surface: surface);
        }

        await Move(chart, (icon.Left + icon.Right) / 2d, (icon.Top + icon.Bottom) / 2d);
        await Paint(chart, surface: surface);
        return (chart, surface, (icon.Left + icon.Right) / 2d, (icon.Top + icon.Bottom) / 2d);
    }

    [Fact]
    public async Task HoveredLegendItemOfAHiddenSeries_LeavesTheOtherSeriesAtFullHoverFactor() {
        var (chart, surface, _, _) = await TwoSeriesWithSecondHidden(hide: true);
        using var _ = surface;

        chart.Instance.HoveredLegendItem.Should().NotBeNull();
        chart.Instance.HoveredSeries.Should().BeNull();
        chart.Instance.Series[0].HoverFactor.Should().Be(1f);
        chart.Instance.Series[1].HoverFactor.Should().Be(1f);
    }

    [Fact]
    public async Task HoveredLegendItemOfAVisibleSeries_DeEmphasizesTheOtherSeries() {
        var (chart, surface, _, _) = await TwoSeriesWithSecondHidden(hide: false);
        using var _ = surface;

        chart.Instance.HoveredSeries.Should().BeSameAs(chart.Instance.Series[1]);
        chart.Instance.Series[0].HoverFactor.Should().Be(NTChartTokens.DeEmphasisOpacity);
        chart.Instance.Series[1].HoverFactor.Should().Be(1f);
    }

    [Fact]
    public async Task HoveredLegendItemOfAHiddenSeries_StillShowsItsOwnHoverStateLayer() {
        var (chart, surface, _, _) = await TwoSeriesWithSecondHidden(hide: true);
        using var _ = surface;

        var item = chart.Instance.HoveredLegendItem!;
        chart.Instance.Series[1].IsLegendItemHovered(item).Should().BeTrue();
    }
}
