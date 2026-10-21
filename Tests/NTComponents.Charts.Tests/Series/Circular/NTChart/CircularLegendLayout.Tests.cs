using System.Reflection;
using Microsoft.AspNetCore.Components;
using NTComponents.Charts.Core;
using SkiaSharp;

namespace NTComponents.Charts.Tests.Series.Circular;

/// <summary>How the chart allocates space to circular series next to a legend and routes legend pointer input to slices.</summary>
public class CircularLegendLayout_Tests : CircularChartTestBase {
    private IRenderedComponent<NTChart<Item>> RenderPieWithLegend(Item[] data, LegendPosition position) => RenderChart(builder => {
        SeriesFragment<NTPieSeries<Item>>(data, new() { ["ShowDataLabels"] = false })(builder);
        builder.OpenComponent<NTLegend<Item>>(100);
        builder.AddAttribute(101, "Position", position);
        builder.CloseComponent();
    });

    private static SKRect LegendArea(IRenderedComponent<NTChart<Item>> chart) =>
        (SKRect)typeof(NTLegend<Item>).GetProperty("LastDrawArea", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(chart.FindComponent<NTLegend<Item>>().Instance)!;

    private static (int Top, int Bottom)? VerticalExtent(SKSurface surface, int x, int startY = 120) {
        using var pixmap = surface.PeekPixels();
        if (IsBackground(pixmap.GetPixelColor(x, startY))) {
            return null;
        }

        var top = startY;
        var bottom = startY;
        while (top > 0 && !IsBackground(pixmap.GetPixelColor(x, top - 1))) {
            top--;
        }
        while (bottom < pixmap.Height - 1 && !IsBackground(pixmap.GetPixelColor(x, bottom + 1))) {
            bottom++;
        }

        return (top, bottom);
    }

    private static float HorizontalRun(SKSurface surface, int startX, int y, int direction) {
        using var pixmap = surface.PeekPixels();
        var x = startX;
        while (x >= 0 && x < pixmap.Width && !IsBackground(pixmap.GetPixelColor(x, y))) {
            x += direction;
        }

        return Math.Abs(x - startX);
    }

    [Fact]
    public async Task PieAlone_FillsThePlotAreaCenteredOnTheChart() {
        var cut = RenderChart<NTPieSeries<Item>>(Items(1, 1, 2), new() { ["ShowDataLabels"] = false });
        using var surface = NewSurface();

        await PaintAsync(cut, surface);

        var extent = VerticalExtent(surface, 200)!.Value;
        extent.Top.Should().BeInRange(7, 10);
        extent.Bottom.Should().BeInRange(290, 293);
    }

    [Theory]
    [InlineData(LegendPosition.Right)]
    [InlineData(LegendPosition.Left)]
    public async Task SideLegend_KeepsThePieCenteredOnTheChartNotOnTheShrunkenPlotArea(LegendPosition position) {
        var cut = RenderPieWithLegend(Items(1, 1, 2), position);
        using var surface = NewSurface();

        await PaintAsync(cut, surface);

        var extent = VerticalExtent(surface, 200)!.Value;
        ((extent.Top + extent.Bottom) / 2f).Should().BeApproximately(150f, 2f, "the pie is centered vertically");
        LegendArea(cut).Width.Should().BeGreaterThan(0);
        var left = HorizontalRun(surface, 200, 150, -1);
        var right = HorizontalRun(surface, 200, 150, 1);
        left.Should().BeApproximately(right, 2f, "the pie is horizontally centered on the chart");
        left.Should().BeApproximately((extent.Bottom - extent.Top) / 2f, 2f, "the pie is circular");
        left.Should().BeLessThan(145f, "the legend takes horizontal room so the radius shrinks");
    }

    [Fact]
    public async Task BottomLegend_ShrinksThePieVerticallyAboveTheLegend() {
        var withoutLegend = RenderChart<NTPieSeries<Item>>(Items(1, 1, 2), new() { ["ShowDataLabels"] = false });
        using var plainSurface = NewSurface();
        await PaintAsync(withoutLegend, plainSurface);
        var cut = RenderPieWithLegend(Items(1, 1, 2), LegendPosition.Bottom);
        using var surface = NewSurface();

        await PaintAsync(cut, surface);

        var plain = VerticalExtent(plainSurface, 200)!.Value;
        var shrunk = VerticalExtent(surface, 200)!.Value;
        var legend = LegendArea(cut);
        legend.Height.Should().BeGreaterThan(0);
        (shrunk.Bottom - shrunk.Top).Should().BeLessThan(plain.Bottom - plain.Top);
        shrunk.Bottom.Should().BeLessThan((int)legend.Top + 1);
    }

    [Fact]
    public async Task ClickingLegendEntry_HidesThatSliceAndRepaintsTheRemainingOnes() {
        var cut = RenderPieWithLegend(Items(1, 1, 2), LegendPosition.Bottom);
        using var surface = NewSurface();
        await PaintAsync(cut, surface);
        var series = cut.FindComponent<NTPieSeries<Item>>().Instance;
        var legend = LegendArea(cut);

        for (var x = legend.Left; x < legend.Right && LegendItems(series).All(e => e.IsVisible); x += 3) {
            await ChartClickAsync(cut, x, legend.MidY);
        }

        var entries = LegendItems(series);
        var hidden = entries.Single(e => !e.IsVisible);
        surface.Canvas.Clear(SKColors.White);
        await PaintAsync(cut, surface);
        Stats(surface, PaletteColors[hidden.Index!.Value]).Count.Should().BeLessThan(2000, "only the small legend icon of the hidden entry keeps its color");
        var remaining = entries.Where(e => e.IsVisible).Select(e => e.Index!.Value).ToArray();
        remaining.Should().HaveCount(2);
        foreach (var index in remaining) {
            Stats(surface, PaletteColors[index]).Count.Should().BeGreaterThan(5000);
        }
    }

    [Fact]
    public async Task PointerOverLegendEntry_HoversThatSliceIndex() {
        var cut = RenderPieWithLegend(Items(1, 1, 2), LegendPosition.Bottom);
        using var surface = NewSurface();
        await PaintAsync(cut, surface);
        var legend = LegendArea(cut);

        for (var x = legend.Left; x < legend.Right && cut.Instance.HoveredPointIndex != 2; x += 3) {
            await HoverAsync(cut, surface, x, legend.MidY);
        }

        cut.Instance.HoveredPointIndex.Should().Be(2);
        cut.Instance.HoveredSeries.Should().BeSameAs(cut.FindComponent<NTPieSeries<Item>>().Instance);
        cut.Instance.HoveredDataPoint!.Name.Should().Be("Item2");
    }
}
