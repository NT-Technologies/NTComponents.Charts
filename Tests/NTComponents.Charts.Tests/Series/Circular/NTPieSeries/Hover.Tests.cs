using NTComponents.Charts.Core.Series;
using Microsoft.AspNetCore.Components;
using NTComponents.Charts.Core;
using SkiaSharp;

namespace NTComponents.Charts.Tests.Series.Circular;

/// <summary>Hover, explosion and dimming behavior of a pie driven through the real chart paint and pointer pipeline.</summary>
public class Hover_Tests : CircularChartTestBase {
    // A bare pie chart has a 5px margin, so the plot area is (5,5)-(395,295): center (200,150), radius 145.
    private static readonly SKPoint Center = new(200, 150);

    private async Task<IRenderedComponent<NTChart<Item>>> PaintedAsync(SKSurface surface, Item[] data, Dictionary<string, object>? attributes = null) {
        var merged = new Dictionary<string, object> { ["ShowDataLabels"] = false };
        foreach (var attribute in attributes ?? []) {
            merged[attribute.Key] = attribute.Value;
        }
        var cut = RenderChart<NTPieSeries<Item>>(data, merged);
        await PaintAsync(cut, surface);
        return cut;
    }

    [Fact]
    public async Task PointerOverSlice_SetsHoveredSeriesIndexAndData() {
        using var surface = NewSurface();
        var data = Items(1, 1, 2);
        var cut = await PaintedAsync(surface, data);
        var point = Polar(Center, 100, 45);

        await HoverAsync(cut, surface, point.X, point.Y);

        cut.Instance.HoveredPointIndex.Should().Be(1);
        cut.Instance.HoveredDataPoint.Should().BeSameAs(data[1]);
        cut.Instance.HoveredSeries.Should().BeSameAs(cut.FindComponent<NTPieSeries<Item>>().Instance);
    }

    [Fact]
    public async Task PointerOverHoleOfDonut_DoesNotHover() {
        using var surface = NewSurface();
        var cut = await PaintedAsync(surface, Items(1, 1), new() { ["InnerRadius"] = 60f });

        await HoverAsync(cut, surface, Center.X, Center.Y);

        cut.Instance.HoveredPointIndex.Should().BeNull();
        cut.Instance.HoveredSeries.Should().BeNull();
    }

    [Fact]
    public async Task HoveredSlice_ExplodesOutwardBeyondTheOriginalRadius() {
        using var surface = NewSurface();
        var cut = await PaintedAsync(surface, Items(1, 1, 2));
        var outside = Polar(Center, 150, -45);
        IsBackground(PixelAt(surface, outside.X, outside.Y)).Should().BeTrue("a non-hovered pie stays within its 145px radius");
        var inside = Polar(Center, 100, -45);

        await HoverAsync(cut, surface, inside.X, inside.Y);
        surface.Canvas.Clear(SKColors.White);
        await PaintAsync(cut, surface);

        IsStateLayered(PixelAt(surface, outside.X, outside.Y), PaletteColors[0]).Should().BeTrue("the hovered slice is displaced by ExplosionDistance (10px) along its mid angle");
    }

    [Fact]
    public async Task HoveredSlice_DoesNotExplodeWhenExplodeOnHoverIsDisabled() {
        using var surface = NewSurface();
        var cut = await PaintedAsync(surface, Items(1, 1, 2), new() { ["ExplodeOnHover"] = false });
        var inside = Polar(Center, 100, -45);
        var outside = Polar(Center, 150, -45);

        await HoverAsync(cut, surface, inside.X, inside.Y);
        surface.Canvas.Clear(SKColors.White);
        await PaintAsync(cut, surface);

        cut.Instance.HoveredPointIndex.Should().Be(0);
        IsBackground(PixelAt(surface, outside.X, outside.Y)).Should().BeTrue();
    }

    [Fact]
    public async Task ExplosionDistance_ControlsHowFarTheHoveredSliceMoves() {
        using var surface = NewSurface();
        var cut = await PaintedAsync(surface, Items(1, 1, 2), new() { ["ExplosionDistance"] = 30f });
        var inside = Polar(Center, 100, -45);

        await HoverAsync(cut, surface, inside.X, inside.Y);
        surface.Canvas.Clear(SKColors.White);
        await PaintAsync(cut, surface);

        var farOutside = Polar(Center, 170, -45);
        IsStateLayered(PixelAt(surface, farOutside.X, farOutside.Y), PaletteColors[0]).Should().BeTrue();
    }

    [Fact]
    public async Task ExplodedSlice_RemainsHittableWhereItNowIsEvenOutsideTheOriginalCircle() {
        using var surface = NewSurface();
        var cut = await PaintedAsync(surface, Items(1, 1, 2));
        var series = cut.FindComponent<NTPieSeries<Item>>().Instance;
        var inside = Polar(Center, 100, -45);
        await HoverAsync(cut, surface, inside.X, inside.Y);
        var areaRadius145 = new SKRect(5, 5, 395, 295);

        series.HitTest(Polar(Center, 148, -45), areaRadius145)!.Value.Index.Should().Be(0);
        series.HitTest(Polar(Center, 148, 45), areaRadius145).Should().BeNull("only the hovered slice moves");
    }

    [Fact]
    public async Task OtherSlices_AreDimmedWhileOneIsHovered() {
        using var surface = NewSurface();
        var cut = await PaintedAsync(surface, Items(1, 1, 2));
        var hover = Polar(Center, 100, -45);
        await HoverAsync(cut, surface, hover.X, hover.Y);
        surface.Canvas.Clear(SKColors.White);

        await PaintAsync(cut, surface);

        var dimmed = PixelAt(surface, Polar(Center, 90, 45).X, Polar(Center, 90, 45).Y);
        // 38% (the Material 3 de-emphasis opacity) of pure green over white.
        var expected = (byte)(255 * (1f - NTChartTokens.DeEmphasisOpacity));
        dimmed.Red.Should().BeInRange((byte)(expected - 4), (byte)(expected + 4));
        dimmed.Green.Should().BeInRange(250, 255);
        dimmed.Blue.Should().BeInRange((byte)(expected - 4), (byte)(expected + 4));
        IsStateLayered(PixelAt(surface, Polar(Center, 90, -45).X + 5, Polar(Center, 90, -45).Y - 5), PaletteColors[0]).Should().BeTrue("the hovered slice keeps full opacity under its state layer");
    }

    [Fact]
    public async Task PointerLeavingTheChart_RestoresTheRestingPie() {
        using var surface = NewSurface();
        var cut = await PaintedAsync(surface, Items(1, 1, 2));
        var resting = Snapshot(surface);
        var hover = Polar(Center, 100, -45);
        await HoverAsync(cut, surface, hover.X, hover.Y);

        await ChartMouseOutAsync(cut);
        surface.Canvas.Clear(SKColors.White);
        await PaintAsync(cut, surface);

        cut.Instance.HoveredSeries.Should().BeNull();
        cut.Instance.HoveredPointIndex.Should().BeNull();
        Snapshot(surface).Should().Equal(resting);
    }

    [Fact]
    public async Task SeriesHoverCallbacks_ReportEnterAndLeaveWithSliceIndex() {
        var entered = new List<int?>();
        var left = new List<int?>();
        using var surface = NewSurface();
        var cut = await PaintedAsync(surface, Items(1, 1), new() {
            ["OnHoverEnter"] = EventCallback.Factory.Create<NTSeriesHoverEnterEventArgs<Item>>(this, args => entered.Add(args.PointIndex)),
            ["OnHoverLeave"] = EventCallback.Factory.Create<NTSeriesHoverLeaveEventArgs<Item>>(this, args => left.Add(args.PointIndex))
        });
        var point = Polar(Center, 100, 135);

        await HoverAsync(cut, surface, point.X, point.Y);
        await ChartMouseOutAsync(cut);
        await cut.InvokeAsync(() => { });

        entered.Should().Equal(1);
        left.Should().Equal(1);
    }
}
