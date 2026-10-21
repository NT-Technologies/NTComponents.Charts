using NTComponents.Charts.Core;
using SkiaSharp;

namespace NTComponents.Charts.Tests.Series.Circular;

/// <summary>A hovered treemap tile gets the Material 3 state layer and keeps its geometry: no scale-up, no blend toward white.</summary>
public class TreeMapHoverStateLayer_Tests : TreeMapTestBase {
    private static readonly Item[] Weighted = Named(("A", 6), ("B", 3), ("C", 2), ("D", 1));

    [Fact]
    public async Task HoveredTile_KeepsItsBoundsAndNeverGrowsOverItsNeighbours() {
        var cut = RenderChart<NTTreeMapSeries<Item>>(Weighted, With(Plain("A", "B", "C", "D"), "AnimationEnabled", false));
        using var surface = NewSurface();
        await PaintAsync(cut, surface);
        var resting = Stats(surface, ColorOf(0), 24).Bounds;
        var neighbour = Stats(surface, ColorOf(1), 6).Bounds;
        await HoverAsync(cut, surface, 100, 150);
        surface.Canvas.Clear(SKColors.White);

        await PaintAsync(cut, surface);

        cut.Instance.HoveredPointIndex.Should().NotBeNull();
        AssertRect(Stats(surface, ColorOf(0), 24).Bounds, resting, 1f);
        AssertRect(Stats(surface, ColorOf(1), 6).Bounds, neighbour, 1f);
    }

    [Fact]
    public async Task HoveredTile_IsTintedByAnOnColorOverlayNotBlendedTowardWhite() {
        var cut = RenderChart<NTTreeMapSeries<Item>>(Weighted, With(Plain("A", "B", "C", "D"), "AnimationEnabled", false));
        using var surface = NewSurface();
        await PaintAsync(cut, surface);
        var resting = PixelAt(surface, 100, 150);
        await HoverAsync(cut, surface, 100, 150);
        surface.Canvas.Clear(SKColors.White);

        await PaintAsync(cut, surface);

        var hovered = PixelAt(surface, 100, 150);
        hovered.Should().NotBe(resting);
        IsStateLayered(hovered, resting).Should().BeTrue("the overlay is at most the 8% hover state layer");
        var expected = NTChartTokens.ContrastingTextColor(resting);
        // Moving toward the on-color: black darkens, white lightens.
        (expected == SKColors.Black ? hovered.Red + hovered.Green + hovered.Blue < resting.Red + resting.Green + resting.Blue : hovered.Red + hovered.Green + hovered.Blue > resting.Red + resting.Green + resting.Blue).Should().BeTrue();
    }
}
