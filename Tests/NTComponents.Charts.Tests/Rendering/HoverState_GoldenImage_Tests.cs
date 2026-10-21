using NTComponents.Charts.Core;
using NTComponents.Charts.Core.Series;
using SkiaSharp;

namespace NTComponents.Charts.Tests.Rendering;

public class HoverState_GoldenImage_Tests : GoldenChartTestBase {
    private static readonly GoldenRow[] _monthly = GoldenData.Monthly();

    [Fact]
    public async Task Line_PointerOnDataPoint_RendersHighlightAndTooltip() {
        var chart = Chart(c => c
            .Cartesian<NTLineSeries<GoldenRow>>(_monthly, GoldenSelectors.Category, GoldenSelectors.A, ("Title", "Alpha"), ("PointStyle", PointStyle.Filled))
            .Cartesian<NTLineSeries<GoldenRow>>(_monthly, GoldenSelectors.Category, GoldenSelectors.B, ("Title", "Beta"))
            .Add<NTTooltip<GoldenRow>>());

        await VerifyAsync("hover-line", chart, hoverAt: (c, plot) => PixelOf(c, plot, "Apr", 22m));
    }

    [Fact]
    public async Task Bar_PointerInsideBar_RendersHighlightAndTooltip() {
        var chart = Chart(c => c
            .Cartesian<NTBarSeries<GoldenRow>>(_monthly, GoldenSelectors.Category, GoldenSelectors.A, ("FadeOtherBarsOnHover", true))
            .Add<NTTooltip<GoldenRow>>());

        await VerifyAsync("hover-bar", chart, hoverAt: (c, plot) => PixelOf(c, plot, "Jun", 10m));
    }

    [Fact]
    public async Task Pie_PointerInsideSlice_RendersExplodedSliceAndTooltip() {
        var chart = Chart(c => c
            .Valued<NTPieSeries<GoldenRow>>(GoldenData.Slices(), GoldenSelectors.Category, GoldenSelectors.A, ("ExplosionOnEntry", false))
            .Add<NTTooltip<GoldenRow>>());

        await VerifyAsync("hover-pie", chart, hoverAt: (_, plot) => new SKPoint(plot.MidX + 50, plot.MidY - 50));
    }

    [Fact]
    public async Task TreeMap_PointerInsideTile_RendersHoverAndTooltip() {
        var chart = Chart(c => c
            .Valued<NTTreeMapSeries<GoldenRow>>(GoldenData.Hierarchy(), GoldenSelectors.Category, GoldenSelectors.Weight,
                ("GroupSelector", (Func<GoldenRow, string?>)(r => r.Group)),
                ("LeafLabelSelector", (Func<GoldenRow, string?>)(r => r.Leaf)),
                ("ColorSelector", GoldenSelectors.TreeMapColor))
            .Add<NTTooltip<GoldenRow>>());

        await VerifyAsync("hover-treemap", chart, hoverAt: (_, plot) => new SKPoint(plot.Left + (plot.Width * 0.2f), plot.Top + (plot.Height * 0.3f)));
    }
}
